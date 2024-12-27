namespace Threadlink.Utilities.Mathematics
{
	using System;
	using UnityEngine;

	public static class Mathematics
	{
		public static float NormalizeBetween(this float target, float min, float max)
		{
			float dif = max - min;
			if (Mathf.Approximately(dif, 0f)) throw new DivideByZeroException();

			return (target - min) / dif;
		}
		public static float NormalizeBetween(this int target, float min, float max)
		{
			float dif = max - min;
			if (Mathf.Approximately(dif, 0f)) throw new DivideByZeroException();

			return (target - min) / dif;
		}

		public static float Denormalize(float normalizedValue, float min, float max) => normalizedValue * (max - min) + min;

		public static float CubicInterpolation(float k0, float k1, float u)
		{
			float u2 = u * u;
			float u3 = u2 * u;
			return k0 * (2 * u3 - 3 * u2 + 1) + k1 * (3 * u2 - 2 * u3);
		}

		public static int Clamp01ToInt(float value) => Mathf.RoundToInt(Mathf.Clamp01(value));
	}
}