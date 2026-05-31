namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct DeterministicAnimatorState : INetworkedComponent
    {
        public uint NetworkTick
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => networkTick;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => networkTick = value;
        }

        private uint networkTick;

        public int boolMask; //Holds 32 flags with zero padding overhead

        public fixed byte TriggerCounters[8]; //Tracks execution counts for up to 8 triggers
        public fixed uint rawDFPs[8];
        public fixed int integers[8];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() { }
    }
}
