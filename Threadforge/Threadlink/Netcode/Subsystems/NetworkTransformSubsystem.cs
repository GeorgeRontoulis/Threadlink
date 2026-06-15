namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;

    public sealed class NetworkTransformSubsystem : NetworkBridgeSubsystem<NetworkTransformSubsystem, DeterministicTransform>
    {
        protected override GamePayloadHeader RequiredPayloadHeader
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GamePayloadHeader.PositionUpdate;
        }
    }
}
