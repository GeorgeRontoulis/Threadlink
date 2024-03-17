namespace Threadlink.Utilities.Collections
{
	using Editor;
	using RNG;
	using System;
	using System.Collections.Generic;
	using UnityEngine;

	public interface IIdentifiable { string LinkID { get; } }

	public struct MatrixPosition
	{
		/// <summary>
		/// Row.
		/// </summary>
		public int R { get; private set; }
		/// <summary>
		/// Column.
		/// </summary>
		public int C { get; private set; }

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
			R = vector.y;
			C = vector.x;
		}

		public readonly bool Equals(MatrixPosition other)
		{
			return R == other.R && C == other.C;
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
		[Flags]
		public enum GridEdge
		{
			Top = 1 << 0,
			Bottom = 1 << 1,
			Left = 1 << 2,
			Right = 1 << 3,
			Corners = 1 << 4,
			All = Top | Bottom | Left | Right | Corners
		}

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

		/// <summary>
		/// Removes the elements of a list from another list. Does not modify the source list 
		/// and assumes that the elements of the rangeToRemove are shared with the source.
		/// </summary>
		/// <typeparam name="T">Type of lists to use.</typeparam>
		/// <param name="source">The source list, a copy of which will be used to remove elements.</param>
		/// <param name="rangeToRemove">The list containing the elements to be removed.</param>
		/// <returns>Returns a new list identical to the source, minus the removed elements.</returns>
		public static List<T> RemoveRange<T>(this List<T> source, List<T> rangeToRemove)
		{
			var result = new List<T>(source);

			int count = rangeToRemove.Count;
			for (int i = 0; i < count; i++) result.Remove(rangeToRemove[i]);

			return result;
		}

		public static bool IsWithinBoundsOf(this int index, Array array)
		{
			if (index < 0 || index >= array.Length) return false;

			return true;
		}

		public static bool IsWithinBoundsOf<T>(this MatrixPosition position, T[,] array)
		{
			if (position.C < 0 || position.R < 0 || position.R >= array.Rows() || position.C >= array.Columns()) return false;

			return true;
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

		public static T[] Filter<T>(this T[] originalArray, Predicate<T> filter, int maxCount = -1, bool shuffle = true)
		{
			var shuffledOriginal = shuffle ? new List<T>(originalArray) : null;
			var filteredList = new List<T>();
			int count = originalArray.Length;
			bool capList = maxCount > 0;

			if (shuffle) shuffledOriginal.Shuffle();

			for (int i = 0; i < count; i++)
			{
				var element = shuffle ? shuffledOriginal[i] : originalArray[i];

				if (filter(element))
				{
					filteredList.Add(element);

					if (capList && filteredList.Count >= maxCount) break;
				}
			}

			int length = filteredList.Count;
			var filteredArray = new T[length];

			for (int i = 0; i < length; i++) filteredArray[i] = filteredList[i];

			return filteredArray;
		}

		public static List<T> Filter<T>(this List<T> originalList, Predicate<T> filter, int maxCount = -1, bool shuffle = true)
		{
			var shuffledOriginal = shuffle ? new List<T>(originalList) : null;
			var filteredList = new List<T>();
			int count = originalList.Count;
			bool capList = maxCount > 0;

			if (shuffle) shuffledOriginal.Shuffle();

			for (int i = 0; i < count; i++)
			{
				var element = shuffle ? shuffledOriginal[i] : originalList[i];

				if (filter(element))
				{
					filteredList.Add(element);

					if (capList && filteredList.Count >= maxCount) break;
				}
			}

			return filteredList;
		}

		public static List<T> Flatten<T>(this T[,] matrix)
		{
			var flattenedList = new List<T>(matrix.Length);
			int z = matrix.Rows();
			int x = matrix.Columns();

			for (int i = 0; i < z; i++)
			{
				for (int j = 0; j < x; j++) flattenedList.Add(matrix[i, j]);
			}

			return flattenedList;
		}
	}
}