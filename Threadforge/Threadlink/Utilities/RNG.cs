namespace Threadlink.Utilities.RNG
{
    using System;

    public static class RNG
    {
        public static uint NextNonZeroUInt(this Random rng)
        {
            Span<byte> bytes = stackalloc byte[4];
            rng.NextBytes(bytes);

            uint value = BitConverter.ToUInt32(bytes);

            return value == 0 ? 1u : value;
        }

        public static ulong NextUInt64(this Random rng)
        {
            Span<byte> buffer = stackalloc byte[8];
            rng.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer);
        }
    }
}
