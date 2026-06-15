namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.ECS;
    using UnityEngine.Scripting;

    [RuntimeComponent, Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct DeterministicPlayableState : INetworkedComponent
    {
        public const int MAX_SYNCED_CLIPS = 8;

        public uint NetworkTick
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => networkTick;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => networkTick = value;
        }

        private uint networkTick;

        // Tracks the maximum active clips blending simultaneously on the Host.
        // 4 is optimal for standard locomotion trees. Increase to 8 if doing 
        // extremely complex 3D blends (e.g., 8-way directional with additive layers).
        public fixed int ActiveClipHashes[MAX_SYNCED_CLIPS];
        public fixed uint RawClipWeights[MAX_SYNCED_CLIPS]; // DFP
        public fixed uint RawClipNormalizedTimes[MAX_SYNCED_CLIPS]; // DFP

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() { }
    }
}