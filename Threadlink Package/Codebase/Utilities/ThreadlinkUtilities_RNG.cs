namespace Threadlink.Utilities.RNG
{
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using UnityEngine;

	/// <summary>
	/// Non-deterministic RNG library using System.Random under the hood.
	/// </summary>
	public static class RNG
	{
		/// <summary>
		/// Deterministic variant using UnityEngine.Random under the hood.
		/// Use this to replicate game state for debugging and testing purposes.
		/// </summary>
		public static class Unity
		{
			public static bool Coinflip => IntegerFromRange(0, 2) > 0;
			public static float NormalizedFloat => Random.value;

			public static int NewRandomSeed
			{
				get
				{
					int seed = RNG.IntegerFromRange(int.MinValue, int.MaxValue);
					Random.InitState(seed);
					return seed;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int IntegerFromRange(int minInclusive, int maxExclusive) => Random.Range(minInclusive, maxExclusive);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static float FloatFromRange(float minInclusive, float maxInclusive) => Random.Range(minInclusive, maxInclusive);
		}

		public static double NextDoubleAugmented => NextDouble * (1.0 + Mathf.Epsilon);
		public static double NextDouble => Generator.NextDouble();
		public static bool Coinflip => IntegerFromRange(0, 2) > 0;

		private static readonly System.Random Generator = new();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IntegerFromRange(int minInclusive, int maxExclusive) => Generator.Next(minInclusive, maxExclusive);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FloatFromRange(float min, float max) => (float)(NextDoubleAugmented * (max - min) + min);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool RandomlyEvaluateWeight(float weight)
		{
			float randomFloat = (float)NextDoubleAugmented;

			return randomFloat < weight || Mathf.Approximately(randomFloat, weight);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Color RandomColor(float alpha)
		{
			static float ColorValue() => (float)NextDoubleAugmented;

			return new(ColorValue(), ColorValue(), ColorValue(), alpha);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RandomPositionInVolume(Transform volumeTransform, Vector3 volumeCenter, Vector3 volumeSize, out Vector3 result)
		{
			float x = volumeSize.x * 0.5f;
			float y = volumeSize.y * 0.5f;
			float z = volumeSize.z * 0.5f;

			volumeSize.x = FloatFromRange(-x, x);
			volumeSize.y = FloatFromRange(-y, y);
			volumeSize.z = FloatFromRange(-z, z);

			result = volumeTransform.position + volumeCenter + volumeTransform.TransformDirection(volumeSize);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Shuffle<T>(this IList<T> list)
		{
			int n = list.Count;
			int k;

			while (n > 1)
			{
				n--;
				k = Generator.Next(n + 1);
				(list[n], list[k]) = (list[k], list[n]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Shuffle<T>(this T[] array)
		{
			int n = array.Length;
			int k;

			while (n > 1)
			{
				n--;
				k = Generator.Next(n + 1);
				(array[n], array[k]) = (array[k], array[n]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,] Shuffle<T>(this T[,] array)
		{
			int rows = array.GetLength(0);
			int columns = array.GetLength(1);

			for (int i = rows - 1; i > 0; i--)
			{
				for (int j = columns - 1; j > 0; j--)
				{
					int m = Generator.Next(i + 1);
					int n = Generator.Next(j + 1);

					(array[m, n], array[i, j]) = (array[i, j], array[m, n]);
				}
			}

			return array;
		}
	}
}