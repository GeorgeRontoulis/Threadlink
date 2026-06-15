namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;

    public sealed class LocalSteamFlowProvider : IFlowProvider
    {
        #region Threadlink Lifecycle API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Discard() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Boot() { }
        #endregion

        #region Public API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HostLobby()
        {
            if (!Netrunner.TryGetSingleton(out var netrunner))
                return;

            netrunner.HostLocally();
            netrunner.PushFlowEvent(FlowEvent.Tag.LobbyCreated, 0);
            netrunner.PushFlowEvent(FlowEvent.Tag.LobbyJoined, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void JoinLobby(ulong _ = 0)
        {
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.ConnectToLocalHost();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AutoJoinHostLobby() => JoinLobby();
        #endregion
    }
}