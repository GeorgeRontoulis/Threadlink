namespace Threadlink.Utilities.Collections
{
	using Editor;
	using RNG;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;
	using UnityEngine.InputSystem.Utilities;
	using String = Text.String;

	public interface IIdentifiable { string LinkID { get; } }

	public struct MatrixPosition : IComparable<MatrixPosition>
	{
		public readonly bool IsValid => R >= 0 && C >= 0;

		/// <summary>
		/// Row.
		/// </summary>
		public int R { get; set; }
		/// <summary>
		/// Column.
		/// </summary>
		public int C { get; set; }

		public MatrixPosition(int invalidIndex = -1)
		{
			R = invalidIndex;
			C = invalidIndex;
		}

		public MatrixPosition(int row, int column)
		{
			R = row;
			C = column;
		}

		public MatrixPosition(Vector2Int vector)
		{
			R = vector.x;
			C = vector.y;
		}

		public readonly bool Equals(MatrixPosition other)
		{
			return R == other.R && C == other.C;
		}

		public override readonly string ToString()
		{
			return String.Construct("[", R.ToString(), ",", C.ToString(), "]");
		}

		public readonly int CompareTo(MatrixPosition other)
		{
			if (C != other.C) return C.CompareTo(other.C);

			return R.CompareTo(other.R);
		}
	}

	[Serializable]
	public sealed class ChunkedArray<T>
	{
		public int Count { get; private set; }

		private int ChunkSize { get; set; }
		private List<T[]> Chunks { get; set; }

		public ChunkedArray(int chunkSize)
		{
			ChunkSize = chunkSize;
			Count = 0;
			Chunks = new();
		}

		public void Add(T item)
		{
			int chunkIndex = Count / ChunkSize;
			int localIndex = Count % ChunkSize;

			if (localIndex == 0) Chunks.Add(new T[ChunkSize]);

			Chunks[chunkIndex][localIndex] = item;
			Count++;
		}

		public void Clear()
		{
			for (int i = Chunks.Count - 1; i >= 0; i--) Chunks[i] = null;

			Chunks.Clear();
			Chunks.TrimExcess();
		}

		public T this[int index]
		{
			get
			{
				int chunkIndex = index / ChunkSize;
				int localIndex = index % ChunkSize;

				return Chunks[chunkIndex][localIndex];
			}
			set
			{
				int chunkIndex = index / ChunkSize;
				int localIndex = index % ChunkSize;

				Chunks[chunkIndex][localIndex] = value;
			}
		}
	}

	public static class Collections
	{
		public static void SortByID(this IIdentifiable[] collection, UnityEngine.Object collectionOwner = null)
		{
			Array.Sort(collection, (x, y) => string.Compare(x.LinkID, y.LinkID));

#if UNITY_EDITOR
			if (collectionOwner != null && EditorUtilities.EditorInOrWillChangeToPlaymode == false)
			{
				EditorUtilities.SetDirty(collectionOwner);
				EditorUtilities.SaveAllAssets();
			}
#endif
		}

		public static void SortByID<T>(this List<T> collection, UnityEngine.Object collectionOwner = null)
		where T : IIdentifiable
		{
			collection.Sort((x, y) => string.Compare(x.LinkID, y.LinkID));

#if UNITY_EDITOR
			if (collectionOwner != null && EditorUtilities.EditorInOrWillChangeToPlaymode == false)
			{
				EditorUtilities.SetDirty(collectionOwner);
				EditorUtilities.SaveAllAssets();
			}
#endif
		}

		public static bool IsRingCell(this MatrixPosition position, int matrixDimensionSize)
		{
			var endIndex = matrixDimensionSize - 1;
			return position.R == 0 || position.R == endIndex || position.C == 0 || position.C == endIndex;
		}

		public static bool IsCornerCell(this MatrixPosition position, int matrixDimensionSize)
		{
			var endIndex = matrixDimensionSize - 1;
			return (position.R == 0 && position.C == 0)
			|| (position.R == 0 && position.C == endIndex)
			|| (position.R == endIndex && position.C == 0)
			|| (position.R == endIndex && position.C == endIndex);
		}

		public static int Rows<T>(this T[,] matrix) { return matrix.GetLength(0); }
		public static int Columns<T>(this T[,] matrix) { return matrix.GetLength(1); }

		public static void For<T>(this T[] collection, Action<T> function)
		{
			int length = collection.Length;

			for (int i = 0; i < length; i++) function(collection[i]);
		}

		public static void For<T>(this List<T> collection, Action<T> function)
		{
			int length = collection.Count;

			for (int i = 0; i < length; i++) function(collection[i]);
		}

		public static int RemapToArrayIndex(this MatrixPosition matrixPosition, int gridDimensionSize)
		{
			return matrixPosition.R * gridDimensionSize + matrixPosition.C;
		}

		public static bool IsWithinBoundsOf(this int index, Array array)
		{
			if (index < 0 || index >= array.Length) return false;

			return true;
		}

		public static bool IsWithinBoundsOf<T>(this int index, List<T> list)
		{
			if (index < 0 || index >= list.Count) return false;

			return true;
		}

		public static bool IsWithinBoundsOf<T>(this MatrixPosition position, T[,] matrix)
		{
			return position.IsValid && position.R < matrix.Rows() && position.C < matrix.Columns();
		}

		public static bool IsWithinBoundsOf(this MatrixPosition position, int matrixRows, int matrixCols)
		{
			return position.IsValid && position.R < matrixRows && position.C < matrixCols;
		}

		public static bool IsWithinBoundsOf<T>(this (int, int) position, T[,] array)
		{
			if (position.Item2 < 0 || position.Item1 < 0 || position.Item1 >= array.Rows() || position.Item2 >= array.Columns()) return false;

			return true;
		}

		/// <summary>
		/// Efficiently removes the element at the target index from this list.
		/// Keep in mind that this method DOES NOT preserve the original order of the list.
		/// If the original order needs to be maintained, please use the standard List methods Remove() and RemoveAt().
		/// </summary>
		/// <typeparam name="T">The type of the list.</typeparam>
		/// <param name="list">The target list.</param>
		/// <param name="index">The index at which to remove the element.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if the provided index is invalid.</exception>
		public static void RemoveEfficiently<T>(this List<T> list, int index)
		{
			int count = list.Count;
			int lastElementIdx = count - 1;

			// If it's the last element, simply remove it
			if (index == lastElementIdx)
			{
				list.RemoveAt(index);
				return;
			}

			// Replace the element at the given index with the last element
			list[index] = list[lastElementIdx];

			// Remove the last element
			list.RemoveAt(lastElementIdx);
		}

		public static T BinarySearch<T>(this IList<T> collection, string id) where T : IIdentifiable
		{
			int BinarySearchIterative()
			{
				int min = 0;
				int max = collection.Count - 1;

				while (min <= max)
				{
					int mid = (min + max) / 2;
					int comparison = string.Compare(id, collection[mid].LinkID);

					if (comparison == 0)
						return mid;
					else if (comparison < 0)
						max = mid - 1;
					else
						min = mid + 1;
				}

				return -1;
			}

			int index = BinarySearchIterative();

			if (index >= 0) return collection[index]; else return default;
		}

		public static T BruteForceSearch<T>(this T[] collection, string id) where T : IIdentifiable
		{
			if (string.IsNullOrEmpty(id)) return default;

			int length = collection.Length;

			for (int i = 0; i < length; i++)
			{
				var element = collection[i];

				if (element == null) continue;
				else if (element.LinkID.Equals(id)) return element;
			}

			return default;
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
			int rows = matrix.Rows();
			int cols = matrix.Columns();

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

		public static List<T> Flatten<T>(this T[,] matrix) { return matrix.Cast<T>().ToList(); }
	}
}