namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;

    public sealed class NetworkAnimationSubsystem : NetworkBridgeSubsystem<NetworkAnimationSubsystem, DeterministicPlayableState>
    {
        protected override GamePayloadHeader RequiredPayloadHeader
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GamePayloadHeader.AnimatorUpdate;
        }
    }
}