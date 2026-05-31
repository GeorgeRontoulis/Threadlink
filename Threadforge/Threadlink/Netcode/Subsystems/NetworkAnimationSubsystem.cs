namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;

    public sealed class NetworkAnimationSubsystem : NetworkedSubsystem<NetworkAnimationSubsystem, DeterministicAnimatorState>
    {
        protected override GamePayloadHeader RequiredPayloadHeader
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GamePayloadHeader.AnimatorUpdate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected unsafe override void ApplyState(Entity entity, DeterministicAnimatorState packetData)
        {
            if (ECSWorld.TryGetSingleton(out var world) && world.TryGetPointer(entity, out DeterministicAnimatorState* animPtr))
            {
                // Unmanaged memory blit directly into the ECS pool
                *animPtr = packetData;
                base.ApplyState(entity, *animPtr);
            }
        }
    }
}