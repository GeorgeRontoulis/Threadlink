namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;

    public sealed class NetworkTransformSubsystem : NetworkedSubsystem<NetworkTransformSubsystem, DeterministicTransform>
    {
        protected override GamePayloadHeader RequiredPayloadHeader
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GamePayloadHeader.PositionUpdate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected unsafe override void ApplyState(Entity entity, DeterministicTransform packetData)
        {
            if (ECSWorld.TryGetSingleton(out var world) && world.TryGetPointer(entity, out DeterministicTransform* transformPtr))
            {
                // Unmanaged memory blit directly into the ECS pool
                *transformPtr = packetData;
                base.ApplyState(entity, *transformPtr);
            }
        }
    }
}
