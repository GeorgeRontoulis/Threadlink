namespace Threadlink.Netcode
{
    using Cysharp.Threading.Tasks;
    using Steamworks;
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using UnityEngine;

    public enum LobbyVisibility : byte
    {
        Invisible,
        Private,
        FriendsOnly,
        Public,
    }

    /// <summary>
    /// Configuration struct passed to the Matchmaker to abstract Steam's KVP filtering.
    /// </summary>
    public struct LobbyRequest
    {
        public LobbyVisibility Visibility;
        public int MaxPlayers;

        public string[] MetadataKeys;
        public string[] MetadataValues;

        public LobbyRequest(LobbyVisibility visibility, int maxPlayers)
        {
            Visibility = visibility;
            MaxPlayers = Math.Max(1, maxPlayers);
            MetadataKeys = null;
            MetadataValues = null;
        }
    }

    public sealed class SteamMatchmaker : ThreadlinkSubsystem<SteamMatchmaker>
    {
        private const byte RETRY_INTERVAL = 2;

        public event Action OnHostLobbyReady;

        private CallResult<LobbyCreated_t> lobbyCreatedCall;
        private CallResult<LobbyMatchList_t> lobbyListCall;
        private Callback<LobbyEnter_t> lobbyEnterCallback;

        private LobbyRequest activeSearchRequest;

        public override void Boot()
        {
            base.Boot();
            lobbyCreatedCall = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyListCall = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);

            //We're assigning this callback to a member to prevent GC from collecting it.
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        #region Host Automation:
        public void CreateLobbyAsync(LobbyRequest request)
        {
            activeSearchRequest = request;

            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.StartHosting();

            var type = request.Visibility switch
            {
                LobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
                LobbyVisibility.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
                LobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
                _ => ELobbyType.k_ELobbyTypeInvisible,
            };

            lobbyCreatedCall.Set(SteamMatchmaking.CreateLobby(type, request.MaxPlayers));
        }

        private void OnLobbyCreated(LobbyCreated_t callback, bool bIOFailure)
        {
            if (callback.m_eResult is not EResult.k_EResultOK || bIOFailure)
            {
                Debug.LogError("[Matchmaker] Failed to create lobby.");
                return;
            }

            var lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobbyID, "Host_ID", SteamUser.GetSteamID().ToString());

            // Stamp specific KVP tags
            if (activeSearchRequest.MetadataKeys != null)
            {
                for (int i = 0; i < activeSearchRequest.MetadataKeys.Length; i++)
                    SteamMatchmaking.SetLobbyData(lobbyID, activeSearchRequest.MetadataKeys[i], activeSearchRequest.MetadataValues[i]);
            }

            OnHostLobbyReady?.Invoke();
        }
        #endregion

        #region Client Automation:
        public void SearchLobbyAsync(LobbyRequest request)
        {
            activeSearchRequest = request;
            var metadataKeys = request.MetadataKeys;
            var metadataValues = request.MetadataValues;
            const ELobbyComparison COMPARISON = ELobbyComparison.k_ELobbyComparisonEqual;

            // Apply specific filters to the backend search
            if (metadataKeys != null)
            {
                int length = metadataKeys.Length;

                for (int i = 0; i < length; i++)
                    SteamMatchmaking.AddRequestLobbyListStringFilter(metadataKeys[i], metadataValues[i], COMPARISON);
            }

            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            lobbyListCall.Set(SteamMatchmaking.RequestLobbyList());
        }

        private void OnLobbyMatchList(LobbyMatchList_t callback, bool bIOFailure)
        {
            if (bIOFailure || callback.m_nLobbiesMatching == 0)
            {
                RetryLobbySearchAsync().Forget();
                return;
            }

            var targetLobby = SteamMatchmaking.GetLobbyByIndex(0);
            SteamMatchmaking.JoinLobby(targetLobby);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async UniTaskVoid RetryLobbySearchAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(RETRY_INTERVAL));
            SearchLobbyAsync(activeSearchRequest);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            var lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            string hostIdString = SteamMatchmaking.GetLobbyData(lobbyID, "Host_ID");

            if (ulong.TryParse(hostIdString, out ulong hostSteamIdRaw) && Netrunner.TryGetSingleton(out var netrunner))
                netrunner.ConnectToHost(hostSteamIdRaw);
        }
        #endregion
    }
}