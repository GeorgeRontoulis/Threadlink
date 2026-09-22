namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.Collections.Generic;
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using Steamworks;

    internal sealed class SteamMultiplayerService : IMultiplayerService
    {
        public event Action<SentinelJoinRequest> JoinRequested;

        private uint AppID { get; }
        private Queue<SentinelJoinRequest> PendingJoinRequests { get; set; } = new();
        private Callback<GameRichPresenceJoinRequested_t> JoinRequestedCallback { get; set; }
        private string CurrentConnectString { get; set; }

        internal SteamMultiplayerService(uint appID)
        {
            AppID = appID;
            JoinRequestedCallback = Callback<GameRichPresenceJoinRequested_t>.Create(
                OnSteamJoinRequested
            );

            CaptureLaunchJoinRequest();
        }

        public UniTask<SentinelResult<IReadOnlyList<SentinelFriend>>> GetFriendsAsync()
        {
            try
            {
                const EFriendFlags FLAGS = EFriendFlags.k_EFriendFlagImmediate;

                var count = SteamFriends.GetFriendCount(FLAGS);

                if (count < 0)
                {
                    return UniTask.FromResult(
                        SentinelResult<IReadOnlyList<SentinelFriend>>.Failure(
                            SentinelError.NativeFailure,
                            "Steam returned an invalid friend count."
                        )
                    );
                }

                var result = new List<SentinelFriend>(count);

                for (var i = 0; i < count; i++)
                {
                    var friendID = SteamFriends.GetFriendByIndex(i, FLAGS);

                    if (!friendID.IsValid())
                        continue;

                    SteamFriends.RequestFriendRichPresence(friendID);

                    var playingThisGame = false;

                    if (SteamFriends.GetFriendGamePlayed(friendID, out var gameInfo))
                    {
                        playingThisGame =
                            gameInfo.m_gameID.IsValid()
                            && gameInfo.m_gameID.AppID().m_AppId == AppID;
                    }

                    var connect = SteamFriends.GetFriendRichPresence(friendID, "connect");
                    var joinable =
                        playingThisGame
                        && SteamJoinPayloadCodec.TryDecode(connect, out var joinPayload);

                    result.Add(
                        new SentinelFriend(
                            friendID.ToString(),
                            SteamFriends.GetFriendPersonaName(friendID),
                            MapPresence(SteamFriends.GetFriendPersonaState(friendID)),
                            playingThisGame,
                            joinable,
                            joinable ? joinPayload : null
                        )
                    );
                }

                return UniTask.FromResult(
                    SentinelResult<IReadOnlyList<SentinelFriend>>.Success(result)
                );
            }
            catch (Exception exception)
            {
                return UniTask.FromResult(
                    SentinelResult<IReadOnlyList<SentinelFriend>>.Failure(
                        SentinelError.NativeFailure,
                        $"Steam friend enumeration failed: {exception.Message}"
                    )
                );
            }
        }

        public UniTask<SentinelResult> SetSessionPresenceAsync(SentinelSessionPresence session)
        {
            if (
                !SteamJoinPayloadCodec.TryEncode(
                    session.JoinPayload,
                    out var connect,
                    out var error
                )
            )
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(SentinelError.InvalidArgument, error)
                );
            }

            if (!SteamFriends.SetRichPresence("connect", connect))
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        "Steam rejected the joinable session's 'connect' rich-presence value."
                    )
                );
            }

            if (
                !SetOptionalPresence("status", session.Status, out error)
                || !SetOptionalPresence("steam_player_group", session.GroupID, out error)
            )
            {
                ClearOwnedPresence();

                return UniTask.FromResult(
                    SentinelResult.Failure(SentinelError.NativeFailure, error)
                );
            }

            var groupSize =
                !string.IsNullOrEmpty(session.GroupID) && session.GroupSize > 0
                    ? session.GroupSize.ToString()
                    : string.Empty;

            if (!SetOptionalPresence("steam_player_group_size", groupSize, out error))
            {
                ClearOwnedPresence();

                return UniTask.FromResult(
                    SentinelResult.Failure(SentinelError.NativeFailure, error)
                );
            }

            CurrentConnectString = connect;
            return UniTask.FromResult(SentinelResult.Success());
        }

        public UniTask<SentinelResult> ClearSessionPresenceAsync()
        {
            return UniTask.FromResult(
                ClearOwnedPresence()
                    ? SentinelResult.Success()
                    : SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        "Steam rejected one or more Sentinel-owned rich-presence clears."
                    )
            );
        }

        public UniTask<SentinelResult> InviteFriendAsync(string friendID)
        {
            if (string.IsNullOrEmpty(CurrentConnectString))
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.Conflict,
                        "No joinable Sentinel session is currently published."
                    )
                );
            }

            if (!ulong.TryParse(friendID, out var rawID))
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.InvalidArgument,
                        $"'{friendID}' is not a valid Steam ID."
                    )
                );
            }

            var steamID = new CSteamID(rawID);

            if (!steamID.IsValid())
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.InvalidArgument,
                        $"'{friendID}' is not a valid Steam user ID."
                    )
                );
            }

            if (
                SteamFriends.GetFriendRelationship(steamID)
                is not EFriendRelationship.k_EFriendRelationshipFriend
            )
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.PermissionDenied,
                        $"Steam user {friendID} is not an immediate friend of the current account."
                    )
                );
            }

            return UniTask.FromResult(
                SteamFriends.InviteUserToGame(steamID, CurrentConnectString)
                    ? SentinelResult.Success()
                    : SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        $"Steam could not send an invite to {friendID}."
                    )
            );
        }

        public bool TryDequeueJoinRequest(out SentinelJoinRequest request)
        {
            if (PendingJoinRequests != null && PendingJoinRequests.Count > 0)
            {
                request = PendingJoinRequests.Dequeue();
                return true;
            }

            request = default;
            return false;
        }

        public void Discard()
        {
            try
            {
                ClearOwnedPresence();
            }
            catch { }

            JoinRequestedCallback?.Dispose();
            JoinRequestedCallback = null;

            PendingJoinRequests?.Clear();
            PendingJoinRequests = null;
            JoinRequested = null;
        }

        private void OnSteamJoinRequested(GameRichPresenceJoinRequested_t callback)
        {
            if (!SteamJoinPayloadCodec.TryDecode(callback.m_rgchConnect, out var payload))
                return;

            var sourceID = default(string);
            var sourceName = default(string);

            if (callback.m_steamIDFriend.IsValid())
            {
                sourceID = callback.m_steamIDFriend.ToString();
                sourceName = SteamFriends.GetFriendPersonaName(callback.m_steamIDFriend);
            }

            PublishJoinRequest(new SentinelJoinRequest(sourceID, sourceName, payload));
        }

        private void CaptureLaunchJoinRequest()
        {
            try
            {
                const int BUFFER = 32768;

                var length = SteamApps.GetLaunchCommandLine(out var commandLine, BUFFER);

                if (length <= 0 || !SteamJoinPayloadCodec.TryDecode(commandLine, out var payload))
                {
                    return;
                }

                PublishJoinRequest(new SentinelJoinRequest(null, null, payload));
            }
            catch
            {
                // Runtime invite callbacks remain available if launch recovery is unavailable.
            }
        }

        private void PublishJoinRequest(SentinelJoinRequest request)
        {
            var handler = JoinRequested;

            if (handler != null)
            {
                handler.Invoke(request);
                return;
            }

            PendingJoinRequests.Enqueue(request);
        }

        private static bool SetOptionalPresence(string key, string value, out string error)
        {
            if (SteamFriends.SetRichPresence(key, value ?? string.Empty))
            {
                error = null;
                return true;
            }

            error = $"Steam rejected rich-presence key '{key}'.";
            return false;
        }

        private bool ClearOwnedPresence()
        {
            var connect = SteamFriends.SetRichPresence("connect", string.Empty);
            var status = SteamFriends.SetRichPresence("status", string.Empty);
            var group = SteamFriends.SetRichPresence("steam_player_group", string.Empty);
            var groupSize = SteamFriends.SetRichPresence("steam_player_group_size", string.Empty);

            CurrentConnectString = null;
            return connect && status && group && groupSize;
        }

        private static SentinelFriendPresence MapPresence(EPersonaState state)
        {
            return state switch
            {
                EPersonaState.k_EPersonaStateOffline => SentinelFriendPresence.Offline,
                EPersonaState.k_EPersonaStateOnline => SentinelFriendPresence.Online,
                EPersonaState.k_EPersonaStateBusy => SentinelFriendPresence.Busy,
                EPersonaState.k_EPersonaStateAway => SentinelFriendPresence.Away,
                EPersonaState.k_EPersonaStateSnooze => SentinelFriendPresence.Snooze,
                EPersonaState.k_EPersonaStateLookingToTrade =>
                    SentinelFriendPresence.LookingToTrade,
                EPersonaState.k_EPersonaStateLookingToPlay => SentinelFriendPresence.LookingToPlay,
                _ => SentinelFriendPresence.Unknown,
            };
        }
    }
}
