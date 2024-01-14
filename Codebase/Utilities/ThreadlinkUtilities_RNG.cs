namespace Threadlink.Utilities.RNG
{
	using System;
	using System.Linq;
	using UnityEngine;
	using System.Collections.Generic;
	using Threadlink.Utilities.UnityLogging;
	using Threadlink.Utilities.Collections;

	public static class RNG
	{
		public static double NextDoubleAugmented => NextDouble * (1.0 + Mathf.Epsilon);
		public static double NextDouble => Generator.NextDouble();
		public static bool Coinflip => IntegerFromRange(0, 2) > 0;

		private static System.Random Generator = new System.Random();

		public static int IntegerFromRange(int minInclusive, int maxExclusive)
		{
			return Generator.Next(minInclusive, maxExclusive);
		}

		public static float FloatFromRange(float min, float max)
		{
			return (float)(NextDoubleAugmented * (max - min) + min);
		}

		public static bool RandomlyEvaluateWeight(float weight)
		{
			float randomFloat = (float)NextDoubleAugmented;

			return randomFloat < weight || Mathf.Approximately(randomFloat, weight);
		}

		public static void Shuffle<T>(this IList<T> list)
		{
			int n = list.Count;

			while (n > 1)
			{
				n--;
				int k = Generator.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

		public static void Shuffle<T>(this T[] array)
		{
			int n = array.Length;

			while (n > 1)
			{
				n--;
				int k = Generator.Next(n + 1);
				T value = array[k];
				array[k] = array[n];
				array[n] = value;
			}
		}

		public static T[,] ShuffleMatrix<T>(T[,] array)
		{
			int rows = array.GetLength(0);
			int columns = array.GetLength(1);

			for (int i = rows - 1; i > 0; i--)
			{
				for (int j = columns - 1; j > 0; j--)
				{
					int m = Generator.Next(i + 1);
					int n = Generator.Next(j + 1);

					T temp = array[i, j];
					array[i, j] = array[m, n];
					array[m, n] = temp;
				}
			}

			return array;
		}

		public static T[,] GetRandomPatch<T>(this T[,] grid)
		{
			int gridWidth = grid.GetLength(0);
			int gridHeight = grid.GetLength(1);

			// Choose random dimensions for the patch
			int patchWidth = IntegerFromRange(1, gridWidth);
			int patchHeight = IntegerFromRange(1, gridHeight);

			// Choose a random start point for the patch
			int startX = IntegerFromRange(0, gridWidth - patchWidth + 1);
			int startY = IntegerFromRange(0, gridHeight - patchHeight + 1);

			// Create a new array to hold the patch
			T[,] patch = new T[patchWidth, patchHeight];

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
			T[,] patch = new T[patchWidth, patchHeight];

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

			if (triIndex == -1)
			{
				UnityConsole.Notify(DebugNotificationType.Error, "triIndex should never be -1");
				return Vector3.zero;
			}

			int indexMul3 = triIndex * 3;

			Vector3 a = verts[tris[indexMul3]];
			Vector3 b = verts[tris[indexMul3 + 1]];
			Vector3 c = verts[tris[indexMul3 + 2]];

			// Generate random barycentric coordinates
			float r = (float)NextDoubleAugmented;
			float s = (float)NextDoubleAugmented;

			if (r + s >= 1)
			{
				r = 1 - r;
				s = 1 - s;
			}

			return a + r * (b - a) + s * (c - a);
		}

		public static float[] GetTriangleSizes(int[] tris, Vector3[] verts)
		{
			int triCount = tris.Length / 3;
			float[] sizes = new float[triCount];

			for (int i = 0; i < triCount; i++)
			{
				int iMul3 = i * 3;

				sizes[i] = .5f * Vector3.Cross(verts[tris[iMul3 + 1]] - verts[tris[iMul3]], verts[tris[iMul3 + 2]] - verts[tris[iMul3]]).magnitude;
			}

			return sizes;
		}

		public static (float[], float) CalculateMeshAreas(float[] sizes)
		{
			float[] cumulativeSizes = new float[sizes.Length];
			float total = 0;

			for (int i = 0; i < sizes.Length; i++)
			{
				total += sizes[i];
				cumulativeSizes[i] = total;
			}

			return (cumulativeSizes, total);
		}
	}
}