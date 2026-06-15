namespace Threadlink.Netcode
{
    using Cysharp.Threading.Tasks;
    using Steamworks;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Threadlink.Core.NativeSubsystems.Scribe;

    public sealed partial class RemoteSteamFlowProvider : IFlowProvider
    {
        // Use a highly unique identifier for your project to avoid AppID 480 collisions
        private const string MATCH_KEY = "ThreadlinkNetcode_Test_Session";
        private const string MATCH_VALUE = "ActiveNetcodeTest";

        private CallResult<LobbyCreated_t> lobbyCreatedCallResult = null;
        private CallResult<LobbyEnter_t> lobbyEnterCallResult = null;
        private CallResult<LobbyMatchList_t> lobbyListCallResult = null;
        private CancellationTokenSource tokenSource = null;

        #region Threadlink Lifecycle API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Discard()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
                tokenSource = null;
            }

            lobbyCreatedCallResult?.Dispose();
            lobbyEnterCallResult?.Dispose();
            lobbyListCallResult?.Dispose();

            lobbyCreatedCallResult = null;
            lobbyEnterCallResult = null;
            lobbyListCallResult = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Boot()
        {
            lobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEnterCallResult = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);
            lobbyListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyListRetrieved);
        }
        #endregion

        #region Event Callbacks:
        private void OnLobbyCreated(LobbyCreated_t result, bool bIOFailure)
        {
            if (bIOFailure
            || result.m_eResult is not EResult.k_EResultOK
            || !Netrunner.TryGetSingleton(out var netrunner))
                return;

            // 1. Tag the lobby so the automated client can find it among generic AppID traffic
            SteamMatchmaking.SetLobbyData(new CSteamID(result.m_ulSteamIDLobby), MATCH_KEY, MATCH_VALUE);

            netrunner.HostRemotely();
            netrunner.PushFlowEvent(FlowEvent.Tag.LobbyCreated, 0);
            netrunner.PushFlowEvent(FlowEvent.Tag.LobbyJoined, 0);
        }

        private void OnLobbyEntered(LobbyEnter_t result, bool bIOFailure)
        {
            if (bIOFailure
            || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess
            || !Netrunner.TryGetSingleton(out var netrunner))
                return;

            netrunner.ConnectToRemoteHost(SteamMatchmaking.GetLobbyOwner(new CSteamID(result.m_ulSteamIDLobby)));
        }

        private void OnLobbyListRetrieved(LobbyMatchList_t result, bool bIOFailure)
        {
            if (bIOFailure || result.m_nLobbiesMatching == 0)
            {
                this.Send("AutoConnect failed: No valid test lobbies found.").ToUnityConsole(DebugType.Warning);
                return;
            }

            // Extract the first valid lobby ID that matched our strict metadata filters
            CSteamID firstLobbyID = SteamMatchmaking.GetLobbyByIndex(0);
            this.Send($"Found test lobby {firstLobbyID}. Attempting to join...").ToUnityConsole();

            JoinLobby((ulong)firstLobbyID);
        }
        #endregion

        #region Public API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HostLobby()
        {
            // Note: Changed to Public or Invisible so matchmaking can discover it, 
            // depending on if you want it visible to the Steam Community. 
            // k_ELobbyTypePublic is required for RequestLobbyList to find it.
            lobbyCreatedCallResult.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, Netrunner.MAX_PLAYERS));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void JoinLobby(ulong lobbyID)
        {
            lobbyEnterCallResult.Set(SteamMatchmaking.JoinLobby((CSteamID)lobbyID));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AutoJoinHostLobby()
        {
            if (tokenSource == null)
            {
                tokenSource = new();
                PollForLobbyAsync(tokenSource.Token).Forget();
            }
        }

        private async UniTaskVoid PollForLobbyAsync(CancellationToken token)
        {
            this.Send("Searching for active test lobbies worldwide...").ToUnityConsole();

            while (!token.IsCancellationRequested)
            {
                SteamMatchmaking.AddRequestLobbyListStringFilter(MATCH_KEY, MATCH_VALUE, ELobbyComparison.k_ELobbyComparisonEqual);
                SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
                SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

                // Dispatch the search
                lobbyListCallResult.Set(SteamMatchmaking.RequestLobbyList());

                // Wait 3 seconds. SuppressCancellationThrow prevents Unity console errors 
                // if we cancel the token while it is actively waiting.
                if (await UniTask.WaitForSeconds(3, cancellationToken: token).SuppressCancellationThrow()) break;
            }
        }
        #endregion
    }
}