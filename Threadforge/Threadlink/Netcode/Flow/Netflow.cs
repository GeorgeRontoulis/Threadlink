namespace Threadlink.Netcode
{
    using Cysharp.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.ECS;
    using Threadlink.Shared;
    using Threadlink.Utilities.Netcode;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using NativeResources = Shared.ThreadlinkIDs.Addressables.NativeResources;

    public sealed class Netflow : ThreadlinkSubsystem<Netflow>, IAddressablesPreloader, IDependencyConsumer<NetflowConfig>
    {
        public bool HostMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Config != null && Config.HostMode;
        }

        private IFlowProvider FlowProvider { get; set; }
        private NetflowConfig Config { get; set; }

        public override void Discard()
        {
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnFlowEventFired -= HandleFlowEvent;

            FlowProvider?.Discard();
            FlowProvider = null;
            Config = null;
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(NetflowConfig input) => (Config = input) != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            if (Threadlink.TryGetSingleton(out var core))
            {
                const NativeResources ID = NativeResources.NetflowConfig;
                return TryConsumeDependency(await core.NativeConfig.LoadNativeResourceAsync<NetflowConfig>(ID));
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnFlowEventFired += HandleFlowEvent;

            if (Config != null && Config.TryGetFlowProvider(out var provider))
            {
                FlowProvider = provider;
                provider.Boot();
            }
            else this.Send("Could not retrieve a Flow Provider for this session!").ToUnityConsole(DebugType.Error);

            base.Boot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HostLobby() => FlowProvider?.HostLobby();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void JoinLobby(ulong lobbyID) => FlowProvider?.JoinLobby(lobbyID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AutoJoinHostLobby() => FlowProvider?.AutoJoinHostLobby();

        private void HandleFlowEvent(in FlowEvent flowEvent)
        {
            // For ALL peers
            ProcessGlobal(in flowEvent);

            // Execute Simulation logic ONLY for the Host
            if (ThreadlinkNetcode.IsHost)
                ProcessAuthoritative(in flowEvent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessGlobal(in FlowEvent flowEvent)
        {
            switch (flowEvent.EventTag)
            {
                case FlowEvent.Tag.LobbyJoined:
                case FlowEvent.Tag.PlayerJoined:
                    this.Send($"Player {flowEvent.PlayerIndex} joined!").ToUnityConsole();
                    break;

                case FlowEvent.Tag.PlayerLeft:
                case FlowEvent.Tag.Disconnected:
                    this.Send($"Player {flowEvent.PlayerIndex} disconnected!").ToUnityConsole();
                    break;
            }
        }

        public static unsafe void BindToScenePlayer(in Entity ecsEntity)
        {
            if (!ECSWorld.TryGetSingleton(out var world)) return;

            var playerGO = UnityEngine.GameObject.FindWithTag("Player");

            if (playerGO != null
            && playerGO.TryGetComponent(out NetworkTransform netTransform)
            && playerGO.TryGetComponent(out NetworkPlayableAnimator netAnimator))
            {
                world.Add<DeterministicTransform>(ecsEntity);
                netTransform.Bind(ecsEntity);

                world.Add<DeterministicPlayableState>(ecsEntity);
                netAnimator.Bind(ecsEntity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ProcessAuthoritative(in FlowEvent flowEvent)
        {
            if (!ECSWorld.TryGetSingleton(out var world)
            || !Networld.TryGetSingleton(out var networld)
            || !Netrunner.TryGetSingleton(out var netrunner)
            || !EntityOwnershipRegistry.TryGetSingleton(out var registry))
            {
                return;
            }

            switch (flowEvent.EventTag)
            {
                case FlowEvent.Tag.LobbyJoined:
                    var newAvatar = networld.CreateAuthoritativeEntity(out int newNetworkID);
                    registry.Bind(newAvatar, flowEvent.PlayerIndex);
                    BindToScenePlayer(newAvatar);
                    this.Send($"Bound to scene player: {newAvatar.AsNetworkEntity()}!").ToUnityConsole();
                    break;

                case FlowEvent.Tag.PlayerJoined:
                    var spawnPayload = new EntitySpawnPayload(1, flowEvent.PlayerIndex, EntitySpawnPayload.ActionType.Spawn, netrunner.CurrentTick);
                    var identity = NetworkPayloadIdentity.ForRPC(GamePayloadHeader.EntitySpawnAction);
                    netrunner.Send(identity, in spawnPayload, NetMsgReliability.Reliable);
                    break;

                case FlowEvent.Tag.PlayerLeft:
                case FlowEvent.Tag.Disconnected:
                    var orphanedEntities = new UnsafeList<Entity>(16, Allocator.Temp);
                    registry.GetAllEntitiesOwnedBy(flowEvent.PlayerIndex, ref orphanedEntities);

                    int length = orphanedEntities.Length;
                    for (int i = 0; i < length; i++)
                    {
                        var entity = orphanedEntities[i];

                        if (world.Has<NetworkEntity>(entity))
                        {
                            if (world.TryGetPointer(entity, out NetworkEntity* netEntityPtr))
                                networld.DestroyNetworkEntity(netEntityPtr->NetworkID);
                        }
                        else world.Destroy(entity);

                        registry.Relinquish(entity);
                    }

                    orphanedEntities.Dispose();
                    break;
            }
        }
    }
}