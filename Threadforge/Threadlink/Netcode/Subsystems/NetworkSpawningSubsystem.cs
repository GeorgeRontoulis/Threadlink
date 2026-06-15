namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.ECS;
    using Threadlink.Utilities.Netcode;

    public sealed class NetworkSpawningSubsystem : ThreadlinkSubsystem<NetworkSpawningSubsystem>
    {
        public override void Boot()
        {
            base.Boot();
            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(GamePayloadHeader.EntitySpawnAction, RPC_ProcessSpawnPayload);
        }

        public override void Discard()
        {
            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(GamePayloadHeader.EntitySpawnAction, null);

            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void RPC_ProcessSpawnPayload(in Entity _, IntPtr dataPtr, int size)
        {
            if (!Netrunner.TryGetSingleton(out var netrunner) || netrunner.IsHost)
                return; // Host executes spawns natively during FlowEvents.

            if (NetworkSerializer.TryGetSingleton(out var serializer)
            && serializer.TryDeserialize(dataPtr, size, out EntitySpawnPayload payload))
            {
                ExecuteSpawnCommand(in payload);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteSpawnCommand(in EntitySpawnPayload payload)
        {
            if (!Networld.TryGetSingleton(out var networld) || !EntityOwnershipRegistry.TryGetSingleton(out var registry))
                return;

            if (payload.Action is EntitySpawnPayload.ActionType.Spawn)
            {
                // 1. Local ECS instantiation mapped strictly to the Host's Authoritative NetworkID
                var proxyEntity = networld.CreateReplicatedEntity(payload.NetworkID);
                registry.Bind(proxyEntity, payload.OwnerIndex);
                Netflow.BindToScenePlayer(proxyEntity);
                this.Send($"Bound proxy {proxyEntity.AsNetworkEntity()} to existing Host object.").ToUnityConsole();
            }
            else if (payload.Action is EntitySpawnPayload.ActionType.Despawn)
            {
                networld.DestroyNetworkEntity(payload.NetworkID);
            }
        }
    }
}