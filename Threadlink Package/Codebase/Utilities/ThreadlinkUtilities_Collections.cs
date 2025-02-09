namespace Threadlink.Utilities.Collections
{
	using Core;
	using RNG;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using Unity.Mathematics;

	public static class Collections
	{
		public static void PopulateWithNewInstances<T>(this IList<T> collection, int instanceCount) where T : new()
		{
			collection.Clear();
			for (int i = 0; i < instanceCount; i++) collection.Add(new T());
		}

		public static bool IsRingCell(this int2 position, int dimSize)
		{
			var endIndex = dimSize - 1;
			return position.x == 0 || position.x == endIndex || position.y == 0 || position.y == endIndex;
		}

		public static bool IsCornerCell(this int2 position, int dimSize)
		{
			var endIndex = dimSize - 1;
			return (position.x == 0 && position.y == 0)
			|| (position.x == 0 && position.y == endIndex)
			|| (position.x == endIndex && position.y == 0)
			|| (position.x == endIndex && position.y == endIndex);
		}

		public static bool IsWithinBoundsOf(this int index, ICollection collection)
		{
			return index >= 0 && index < collection.Count;
		}

		public static int BruteForceSearch<T>(this IReadOnlyList<T> collection, string key) where T : ILinkable<string>
		{
			if (string.IsNullOrEmpty(key)) return -1;

			int length = collection.Count;

			for (int i = 0; i < length; i++)
			{
				var element = collection[i];

				if (element == null || string.IsNullOrEmpty(element.LinkID)) continue;
				else if (element.LinkID.Equals(key)) return i;
			}

			return -1;
		}

		public static bool BruteForceSearch<T>(this IReadOnlyList<T> collection, Func<T, bool> filter, out T result)
		{
			int length = collection.Count;

			for (int i = 0; i < length; i++)
			{
				var element = collection[i];

				if (element == null) continue;
				else if (filter(element))
				{
					result = element;
					return true;
				}
			}

			result = default;
			return false;
		}

		public static void Filter<T>(this T[] source, List<T> destination, Func<T, bool> filter, int maxCount = -1, bool shuffle = true)
		{
			destination.Clear();

			if (shuffle)
			{
				var shuffledSource = new List<T>(source);
				shuffledSource.Shuffle();

				destination.AddRange(shuffledSource.Where(filter));
			}
			else destination.AddRange(source.Where(filter));

			if (maxCount > 0 && destination.Count > 0 && destination.Count > maxCount)
				destination.RemoveRange(maxCount, destination.Count - maxCount);

			destination.TrimExcess();
		}

		public static void Filter<T>(this List<T> source, List<T> destination, Func<T, bool> filter, int maxCount = -1, bool shuffle = true)
		{
			destination.Clear();

			if (shuffle)
			{
				var shuffledSource = new List<T>(source);
				shuffledSource.Shuffle();

				destination.AddRange(shuffledSource.Where(filter));
			}
			else destination.AddRange(source.Where(filter));

			if (maxCount > 0 && destination.Count > 0 && destination.Count > maxCount)
				destination.RemoveRange(maxCount, destination.Count - maxCount);

			destination.TrimExcess();
		}

		public static List<T> Filter<T>(this List<T> source, Func<T, bool> filter, int maxCount = -1, bool shuffle = true)
		{
			var result = new List<T>(maxCount > 0 ? maxCount : 0);

			if (shuffle)
			{
				var shuffledSource = new List<T>(source);
				shuffledSource.Shuffle();

				result.AddRange(shuffledSource.Where(filter));
			}
			else result.AddRange(source.Where(filter));

			if (maxCount > 0 && result.Count > maxCount && result.Count > maxCount)
				result.RemoveRange(maxCount, result.Count - maxCount);

			result.TrimExcess();

			return result;
		}

		public static List<T> Filter<T>(this T[,] matrix, Func<T, bool> filter, int maxCount = -1, bool shuffle = true)
		{
			var result = new List<T>(maxCount > 0 ? maxCount : 0);
			int rows = matrix.GetLength(0);
			int cols = matrix.GetLength(1);

			void FilterMatrix(T[,] matrix)
			{
				for (int i = 0; i < rows; i++)
				{
					for (int j = 0; j < cols; j++)
					{
						var element = matrix[i, j];

						if (filter.Invoke(element)) result.Add(element);
					}
				}
			}

			if (shuffle)
			{
				var shuffledSource = new T[rows, cols];

				Array.Copy(matrix, shuffledSource, matrix.Length);
				shuffledSource.Shuffle();

				FilterMatrix(shuffledSource);
			}
			else FilterMatrix(matrix);

			if (maxCount > 0 && result.Count > maxCount && result.Count > maxCount)
				result.RemoveRange(maxCount, result.Count - maxCount);

			result.TrimExcess();

			return result;
		}

		public static void Filter<T>(this T[,] matrix, Func<T, bool> filter, List<T> destination, int maxCount = -1, bool shuffle = true)
		{
			destination.Clear();

			int rows = matrix.GetLength(0);
			int cols = matrix.GetLength(1);

			void FilterMatrix(T[,] matrix)
			{
				for (int i = 0; i < rows; i++)
				{
					for (int j = 0; j < cols; j++)
					{
						var element = matrix[i, j];

						if (filter.Invoke(element)) destination.Add(element);
					}
				}
			}

			if (shuffle)
			{
				var shuffledSource = new T[rows, cols];

				Array.Copy(matrix, shuffledSource, matrix.Length);
				shuffledSource.Shuffle();

				FilterMatrix(shuffledSource);
			}
			else FilterMatrix(matrix);

			if (maxCount > 0 && destination.Count > maxCount && destination.Count > maxCount)
				destination.RemoveRange(maxCount, destination.Count - maxCount);

			destination.TrimExcess();
		}

		public static List<T> Flatten<T>(this T[,] matrix) => matrix.Cast<T>().ToList();
	}
}