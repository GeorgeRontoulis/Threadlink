namespace Threadlink.Utilities.Mathematics
{
	using System;
	using System.Runtime.CompilerServices;
	using Unity.Mathematics;
	using UnityEngine;

	public static class Mathematics
	{
		public static int2 ToInt2(this Vector2Int target) => new(target.x, target.y);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float NormalizeBetween(this float target, float min, float max)
		{
			float dif = max - min;
			if (Mathf.Approximately(dif, 0f)) throw new DivideByZeroException();

			return (target - min) / dif;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float NormalizeBetween(this int target, float min, float max)
		{
			float dif = max - min;
			if (Mathf.Approximately(dif, 0f)) throw new DivideByZeroException();

			return (target - min) / dif;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Denormalize(float normalizedValue, float min, float max) => normalizedValue * (max - min) + min;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CubicInterpolation(float k0, float k1, float u)
		{
			float u2 = u * u;
			float u3 = u2 * u;
			return k0 * (2 * u3 - 3 * u2 + 1) + k1 * (3 * u2 - 2 * u3);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp01ToInt(float value) => Mathf.RoundToInt(Mathf.Clamp01(value));
	}
}