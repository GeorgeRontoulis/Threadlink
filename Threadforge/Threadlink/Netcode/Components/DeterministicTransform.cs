namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.ECS;
    using UnityEngine.Scripting;

    [RuntimeComponent, Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeterministicTransform : INetworkedComponent
    {
        public uint NetworkTick
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => networkTick;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => networkTick = value;
        }

        public uint rawPositionX;
        public uint rawPositionY;
        public uint rawPositionZ;

        public uint rawRotationX;
        public uint rawRotationY;
        public uint rawRotationZ;
        public uint rawRotationW;

        public uint rawScaleX;
        public uint rawScaleY;
        public uint rawScaleZ;

        public uint networkTick;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() { }
    }
}