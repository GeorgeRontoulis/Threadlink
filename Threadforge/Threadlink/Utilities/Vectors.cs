namespace Threadlink.Utilities.Vectors
{
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;
    using UnityEngine;
    using Utilities.Mathematics;

    public static class Vectors
    {
        public static readonly Vector3 XZ = new(1f, 0f, 1f);

        /// <summary>
        /// Clamps the vector's magnitude to [<paramref name="min"/>, <paramref name="max"/>] while preserving direction.
        /// If the vector is (near) zero, returns <see cref="Zero"/> (can't enforce a min without a direction).
        /// </summary>
        public static Vector3 Clamp(this Vector3 v, float min, float max)
        {
            // Normalize bounds
            if (min > max)
                (min, max) = (max, min);

            min = math.max(0f, min);

            float mag = v.magnitude;

            if (mag < Vector3.kEpsilon)
                return Vector3.zero;

            float target = math.clamp(mag, min, max);
            return v * (target / mag);
        }

        /// <summary>
        /// Clamps the vector's magnitude to [<paramref name="min"/>, <paramref name="max"/>] while preserving direction.
        /// If <paramref name="v"/> is zero it uses a fallback direction to honor <paramref name="min"/>.
        /// </summary>
        public static Vector3 ClampWithFallback(this Vector3 v, float min, float max, Vector3 fallbackDirection)
        {
            if (min > max)
                (min, max) = (max, min);

            min = math.max(0f, min);

            float mag = v.magnitude;

            if (mag < Vector3.kEpsilon)
            {
                var dir = fallbackDirection.sqrMagnitude > 0f ? fallbackDirection.normalized : Vector3.zero;
                return dir * math.clamp(0f, min, max);
            }

            return v * (math.clamp(mag, min, max) / mag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSimilarTo(this Vector3 a, Vector3 b)
        {
            return a.x.IsSimilarTo(b.x) && a.y.IsSimilarTo(b.y) && a.z.IsSimilarTo(b.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 VectorTo(this Transform start, Transform end) => end.localPosition - start.localPosition;
    }
}
