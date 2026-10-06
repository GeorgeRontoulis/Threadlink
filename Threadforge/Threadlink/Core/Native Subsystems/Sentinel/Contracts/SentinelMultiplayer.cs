namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public enum SentinelFriendPresence : byte
    {
        Unknown = 0,
        Offline,
        Online,
        Busy,
        Away,
        Snooze,
        LookingToTrade,
        LookingToPlay,
    }

    public readonly struct SentinelFriend
    {
        public string ID { get; }
        public string DisplayName { get; }
        public SentinelFriendPresence Presence { get; }
        public bool IsPlayingThisGame { get; }
        public bool IsJoinable { get; }
        public string JoinPayload { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SentinelFriend(string id, string displayName, SentinelFriendPresence presence,
        bool isPlayingThisGame, bool isJoinable, string joinPayload)
        {
            ID = id;
            DisplayName = displayName;
            Presence = presence;
            IsPlayingThisGame = isPlayingThisGame;
            IsJoinable = isJoinable;
            JoinPayload = joinPayload;
        }
    }

    public readonly struct SentinelSessionPresence
    {
        /// <summary>Opaque public join payload. Null or empty makes presence non-joinable.</summary>
        public string JoinPayload { get; }
        public string Status { get; }
        public string GroupID { get; }
        public int GroupSize { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SentinelSessionPresence(string joinPayload, string status = null, string groupID = null, int groupSize = 0)
        {
            JoinPayload = joinPayload;
            Status = status;
            GroupID = groupID;
            GroupSize = groupSize;
        }
    }

    public readonly struct SentinelJoinRequest
    {
        public string SourceUserID { get; }
        public string SourceDisplayName { get; }
        public string JoinPayload { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SentinelJoinRequest(string sourceUserID, string sourceDisplayName, string joinPayload)
        {
            SourceUserID = sourceUserID;
            SourceDisplayName = sourceDisplayName;
            JoinPayload = joinPayload;
        }
    }

    /// <summary>
    /// Platform-native social/session affordances only.
    /// Networking owns JoinPayload serialization and interpretation.
    /// </summary>
    public interface IMultiplayerService : ISentinelService
    {
        event Action<SentinelJoinRequest> JoinRequested;

        UniTask<SentinelResult<IReadOnlyList<SentinelFriend>>> GetFriendsAsync();
        UniTask<SentinelResult> SetSessionPresenceAsync(SentinelSessionPresence session);
        UniTask<SentinelResult> ClearSessionPresenceAsync();
        UniTask<SentinelResult> InviteFriendAsync(string friendID);
        /// <summary>Send a private opaque join payload to one friend without publishing it in presence.</summary>
        UniTask<SentinelResult> InviteFriendAsync(string friendID, string joinPayload);
        bool TryDequeueJoinRequest(out SentinelJoinRequest request);
    }
}
