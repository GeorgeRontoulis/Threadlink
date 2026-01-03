namespace Threadlink.Utilities.Mathematics
{
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;

    public static class Mathematics
    {
        public const float TOLERANCE_FACTOR = 1E-6f;

        /// <summary>
        /// Float similarity comparison method using 1E-6f as tolerance.
        /// Uses <see cref="math"/> for the internal operations. 
        /// </summary>
        /// <param name="a">The number to compare to the other number.</param>
        /// <param name="b">The other number.</param>
        /// <returns><see langword="true"/> if the two numbers are similar. <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSimilarTo(this float a, float b)
        {
            if (a is float.NaN || b is float.NaN)
                return false;

            if (a == b) return true;

            return math.abs(a - b) <= math.max(TOLERANCE_FACTOR * math.max(math.abs(a), math.abs(b)), TOLERANCE_FACTOR);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MoveTowards(this float current, float target, float maxDelta)
        {
            if (math.abs(target - current) <= maxDelta)
                return target;

            return current + math.sign(target - current) * maxDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ToUVCoordinates(this int2 input, int2 dimensions)
        {
            return new(input.x / (float)dimensions.x, input.y / (float)dimensions.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ToScreenPosition(this float2 input, float2 dimensions)
        {
            return new(input.x * dimensions.x, input.y * dimensions.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CubicInterpolation(float3 k0, float3 k1, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return k0 * (2 * u3 - 3 * u2 + 1) + k1 * (3 * u2 - 2 * u3);
        }
    }
}