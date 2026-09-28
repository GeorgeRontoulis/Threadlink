namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Between Unity's float types and the deterministic ones. To fixed point: for authored data (serialized fields,
    /// literals) and setup, which are the same bits on every machine. To float: for presentation only, since a float must
    /// never flow back into simulated state.
    /// </summary>
    public static class FixedPointConversions
    {
        #region To fixed point:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP ToFP(this float value) => (FP)value;

        public static FPVector2 ToFP(this Vector2 value) => new((FP)value.x, (FP)value.y);

        public static FPVector3 ToFP(this Vector3 value) => new((FP)value.x, (FP)value.y, (FP)value.z);

        public static FPVector2 ToFP(this Vector2Int value) => new(value.x, value.y);

        public static FPVector3 ToFP(this Vector3Int value) => new(value.x, value.y, value.z);

        /// <summary>Renormalized after conversion, since rounding each component leaves it a hair off unit length.</summary>
        public static FPQuaternion ToFP(this Quaternion value) => new FPQuaternion((FP)value.x, (FP)value.y, (FP)value.z, (FP)value.w).Normalized;
        #endregion

        #region To float, for presentation:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(this FP value) => (float)value;

        public static Vector2 ToVector2(this FPVector2 value) => new((float)value.X, (float)value.Y);

        public static Vector3 ToVector3(this FPVector3 value) => new((float)value.X, (float)value.Y, (float)value.Z);

        /// <summary>In the xy plane, z zero: a 2D position for a transform.</summary>
        public static Vector3 ToVector3(this FPVector2 value) => new((float)value.X, (float)value.Y, 0f);

        public static Quaternion ToQuaternion(this FPQuaternion value) => new((float)value.X, (float)value.Y, (float)value.Z, (float)value.W);
        #endregion
    }
}
