namespace Threadlink.Utilities.RNG
{
	using System.Collections.Generic;
	using UnityEngine;

	public interface IRNGWeighted { public bool RandomWeightEvaluation { get; } }

	public static class RNG
	{
		public static double NextDoubleAugmented => NextDouble * (1.0 + Mathf.Epsilon);
		public static double NextDouble => Generator.NextDouble();
		public static bool Coinflip => IntegerFromRange(0, 2) > 0;

		private static readonly System.Random Generator = new();

		public static int IntegerFromRange(int minInclusive, int maxExclusive) => Generator.Next(minInclusive, maxExclusive);
		public static float FloatFromRange(float min, float max) => (float)(NextDoubleAugmented * (max - min) + min);

		public static bool RandomlyEvaluateWeight(float weight)
		{
			float randomFloat = (float)NextDoubleAugmented;

			return randomFloat < weight || Mathf.Approximately(randomFloat, weight);
		}

		public static Color RandomColor(float alpha)
		{
			static float ColorValue() => (float)NextDoubleAugmented;

			return new(ColorValue(), ColorValue(), ColorValue(), alpha);
		}

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