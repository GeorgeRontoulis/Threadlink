namespace Threadlink.Utilities.Collections
{
	using Core;
	using RNG;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;

	public static class Collections
	{
		public static bool IsWithinBoundsOf(this int index, ICollection collection)
		{
			return index >= 0 && index < collection.Count;
		}

		public static int BruteForceSearch<T>(this IReadOnlyList<T> collection, string key) where T : INamable
		{
			if (string.IsNullOrEmpty(key)) return -1;

			int length = collection.Count;

			for (int i = 0; i < length; i++)
			{
				var element = collection[i];

				if (element == null || string.IsNullOrEmpty(element.Name)) continue;
				else if (element.Name.Equals(key)) return i;
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

		public static int BinarySearch<T>(this IReadOnlyList<T> collection, string key) where T : INamable
		{
			int left = 0;
			int right = collection.Count - 1;

			while (left <= right)
			{
				int mid = left + ((right - left) >> 1); // Avoids potential overflow

				int comparison = string.Compare(collection[mid].Name, key, StringComparison.Ordinal);

				if (comparison == 0) return mid; // Match found

				if (comparison < 0)
					left = mid + 1; // Search right half
				else
					right = mid - 1; // Search left half
			}

			return ~left; // Not found, return bitwise complement of insertion point
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