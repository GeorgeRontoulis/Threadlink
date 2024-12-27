namespace Threadlink.Utilities.RNG
{
	using Collections;
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

			while (n > 1)
			{
				n--;
				int k = Generator.Next(n + 1);
				(list[n], list[k]) = (list[k], list[n]);
			}
		}

		public static void Shuffle<T>(this T[] array)
		{
			int n = array.Length;

			while (n > 1)
			{
				n--;
				int k = Generator.Next(n + 1);
				(array[n], array[k]) = (array[k], array[n]);
			}
		}

		public static T[,] Shuffle<T>(this T[,] array)
		{
			int rows = array.Rows();
			int columns = array.Columns();

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

		public static T[,] GetRandomPatch<T>(this T[,] grid)
		{
			int gridWidth = grid.Rows();
			int gridHeight = grid.Columns();

			// Choose random dimensions for the patch
			int patchWidth = IntegerFromRange(1, gridWidth);
			int patchHeight = IntegerFromRange(1, gridHeight);

			// Choose a random start point for the patch
			int startX = IntegerFromRange(0, gridWidth - patchWidth + 1);
			int startY = IntegerFromRange(0, gridHeight - patchHeight + 1);

			// Create a new array to hold the patch
			var patch = new T[patchWidth, patchHeight];

			// Fill the new array with elements from the grid
			for (int i = 0; i < patchWidth; i++)
			{
				for (int j = 0; j < patchHeight; j++) patch[i, j] = grid[startX + i, startY + j];
			}

			return patch;
		}

		public static T[,] GetRandomPatch<T>(this T[,] grid, Vector2Int maxPatchSize)
		{
			int gridWidth = grid.Columns();
			int gridHeight = grid.Rows();

			// Choose random dimensions for the patch
			int patchWidth = IntegerFromRange(2, maxPatchSize.x + 1);
			int patchHeight = IntegerFromRange(2, maxPatchSize.y + 1);

			// Choose a random start point for the patch
			int startX = IntegerFromRange(0, gridWidth - patchWidth + 1);
			int startY = IntegerFromRange(0, gridHeight - patchHeight + 1);

			// Create a new array to hold the patch
			var patch = new T[patchWidth, patchHeight];

			// Fill the new array with elements from the grid
			for (int i = 0; i < patchWidth; i++)
			{
				for (int j = 0; j < patchHeight; j++) patch[i, j] = grid[startX + i, startY + j];
			}

			return patch;
		}

		public static Vector3 GetRandomPointOnMesh(int[] tris, Vector3[] verts, float[] sizes, float[] cumulativeSizes, float total)
		{
			float randomsample = (float)NextDoubleAugmented * total;
			int triIndex = -1;

			int length = sizes.Length;

			for (int i = 0; i < length; i++)
			{
				if (randomsample <= cumulativeSizes[i])
				{
					triIndex = i;
					break;
				}
			}

			if (triIndex == -1) throw new System.ArgumentException("triIndex should never be -1");

			int indexMul3 = triIndex * 3;
			var a = verts[tris[indexMul3]];

			// Generate random barycentric coordinates
			float r = (float)NextDoubleAugmented;
			float s = (float)NextDoubleAugmented;

			if (r + s >= 1)
			{
				r = 1 - r;
				s = 1 - s;
			}

			return a + r * (verts[tris[indexMul3 + 1]] - a) + s * (verts[tris[indexMul3 + 2]] - a);
		}
	}
}