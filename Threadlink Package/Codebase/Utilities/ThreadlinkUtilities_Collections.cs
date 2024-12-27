namespace Threadlink.Utilities.Collections
{
	using Core;
	using RNG;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;

#if UNITY_EDITOR
	using UnityEditor;
#endif

	public interface IRegistryElement : IIdentifiable<string>
	{
		/// <summary>
		/// Callback invoked right before the registry containing this element is discarded.
		/// </summary>
		public void OnBeforeRegistryDiscarded();

		/// <summary>
		/// Callback invoked right before this entity is retrieved from the registry to be utilized in the runtime.
		/// </summary>
		public void OnBeforeUtilized();

		/// <summary>
		/// Callback invoked when this entity is flagged as available.
		/// </summary>
		public void OnSetToAvailable();
	}

	public interface IRegistriesManager
	{
		public Dictionary<string, ThreadlinkRegistry> Registries { get; set; }

		public void InitializeRegistries() => Registries = new();

		public void DiscardAllRegistries()
		{
			foreach (var registry in Registries.Values) registry.Discard(true);

			Registries.Clear();
			Registries.TrimExcess();
			Registries = null;
		}

		public void ClearAllRegistries()
		{
			foreach (var registry in Registries.Values) registry.Discard();

			Registries.Clear();
			Registries.TrimExcess();
		}

		public bool TryRegister(IRegistryElement target, string registryID, int initialCapacity = 100)
		{
			if (Registries.TryGetValue(registryID, out var registry)) return registry.TryRegister(target);
			else
			{
				Registries.Add(registryID, new(initialCapacity));

				return Registries[registryID].TryRegister(target);
			}
		}

		public bool RetrieveFrom<T>(string registryID, out T retrievedElement) where T : IRegistryElement
		{
			var result = default(T);
			bool found = Registries.TryGetValue(registryID, out var registry) && registry.TryRetrieve(out result);

			retrievedElement = found ? result : default;
			return found;
		}

		public void SetAllToAvailableState()
		{
			foreach (var entry in Registries) entry.Value.SetAllToAvailableState();
		}
	}

	public sealed class ThreadlinkRegistry
	{
		private Queue<IRegistryElement> AvailableElements { get; set; }
		private Queue<IRegistryElement> UtilizedElements { get; set; }

		private delegate void VoidDelegate();

		private event VoidDelegate OnBeforeDiscarded = null;
		private event VoidDelegate OnAllSetToUnitilizedState = null;

		public ThreadlinkRegistry(int capacity)
		{
			AvailableElements = new(capacity);
			UtilizedElements = new(capacity);
		}

		public void Discard(bool nullifyCollections = false)
		{
			OnBeforeDiscarded?.Invoke();
			Clear(true);

			OnAllSetToUnitilizedState = null;
			OnBeforeDiscarded = null;

			if (nullifyCollections)
			{
				AvailableElements = null;
				UtilizedElements = null;
			}
		}

		public void Clear(bool trimCollections = false)
		{
			AvailableElements.Clear();
			UtilizedElements.Clear();

			if (trimCollections)
			{
				AvailableElements.TrimExcess();
				UtilizedElements.TrimExcess();
			}
		}

		public bool TryRetrieve<T>(out T result) where T : IRegistryElement
		{
			if (AvailableElements.TryDequeue(out var dequedElement))
			{
				if (UtilizedElements.Contains(dequedElement) == false)
					UtilizedElements.Enqueue(dequedElement);

				dequedElement.OnBeforeUtilized();

				result = (T)dequedElement;
				return true;
			}
			else
			{
				result = default;
				return false;
			}
		}

		public bool TryRegister(IRegistryElement element)
		{
			if (UtilizedElements.Contains(element) == false)
			{
				void OnElementSetToAvailable()
				{
					if (AvailableElements.Contains(element) == false)
						AvailableElements.Enqueue(element);

					element.OnSetToAvailable();
				}

				UtilizedElements.Enqueue(element);
				OnBeforeDiscarded += element.OnBeforeRegistryDiscarded;
				OnAllSetToUnitilizedState += OnElementSetToAvailable;
			}

			return true;
		}

		public void SetAllToAvailableState()
		{
			UtilizedElements.Clear();
			OnAllSetToUnitilizedState?.Invoke();
		}
	}

	[Serializable]
	public sealed class ChunkedArray<T>
	{
		public int Count { get; private set; }

		private int ChunkSize { get; set; }
		private List<T[]> Chunks { get; set; }

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
	}

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

		public override readonly int GetHashCode() => HashCode.Combine(R, C);
		public readonly bool Equals(MatrixPosition other) => R == other.R && C == other.C;
		public override readonly string ToString() => $"[{R},{C}]";

		public readonly int CompareTo(MatrixPosition other)
		{
			if (C != other.C) return C.CompareTo(other.C);

			return R.CompareTo(other.R);
		}
	}

	/// <summary>
	/// Unity can't serialize Dictionary so here's a custom wrapper that does.
	/// </summary>
	[Serializable]
	public sealed class SerializedDictionary<K, V> : SerializedDictionary<K, V, K, V>
	{
		/// <summary>
		/// Conversion to serialize a key
		/// </summary>
		/// <param name="key">The key to serialize</param>
		/// <returns>The Key that has been serialized</returns>
		public override K SerializeKey(K key) => key;

		/// <summary>
		/// Conversion to serialize a value
		/// </summary>
		/// <param name="val">The value</param>
		/// <returns>The value</returns>
		public override V SerializeValue(V val) => val;

		/// <summary>
		/// Conversion to serialize a key
		/// </summary>
		/// <param name="key">The key to serialize</param>
		/// <returns>The Key that has been serialized</returns>
		public override K DeserializeKey(K key) => key;

		/// <summary>
		/// Conversion to serialize a value
		/// </summary>
		/// <param name="val">The value</param>
		/// <returns>The value</returns>
		public override V DeserializeValue(V val) => val;
	}

	/// <summary>
	/// Dictionary that can serialize keys and values as other types
	/// </summary>
	/// <typeparam name="K">The key type</typeparam>
	/// <typeparam name="V">The value type</typeparam>
	/// <typeparam name="SK">The type which the key will be serialized for</typeparam>
	/// <typeparam name="SV">The type which the value will be serialized for</typeparam>
	[Serializable]
	public abstract class SerializedDictionary<K, V, SK, SV> : Dictionary<K, V>, ISerializationCallbackReceiver
	{
		[SerializeField] private List<SK> m_Keys = new();
		[SerializeField] private List<SV> m_Values = new();

		/// <summary>
		/// From <see cref="K"/> to <see cref="SK"/>
		/// </summary>
		/// <param name="key">They key in <see cref="K"/></param>
		/// <returns>The key in <see cref="SK"/></returns>
		public abstract SK SerializeKey(K key);

		/// <summary>
		/// From <see cref="V"/> to <see cref="SV"/>
		/// </summary>
		/// <param name="value">The value in <see cref="V"/></param>
		/// <returns>The value in <see cref="SV"/></returns>
		public abstract SV SerializeValue(V value);


		/// <summary>
		/// From <see cref="SK"/> to <see cref="K"/>
		/// </summary>
		/// <param name="serializedKey">They key in <see cref="SK"/></param>
		/// <returns>The key in <see cref="K"/></returns>
		public abstract K DeserializeKey(SK serializedKey);

		/// <summary>
		/// From <see cref="SV"/> to <see cref="V"/>
		/// </summary>
		/// <param name="serializedValue">The value in <see cref="SV"/></param>
		/// <returns>The value in <see cref="V"/></returns>
		public abstract V DeserializeValue(SV serializedValue);

		/// <summary>
		/// OnBeforeSerialize implementation.
		/// </summary>
		public void OnBeforeSerialize()
		{
			m_Keys.Clear();
			m_Values.Clear();

			foreach (var kvp in this)
			{
				m_Keys.Add(SerializeKey(kvp.Key));
				m_Values.Add(SerializeValue(kvp.Value));
			}
		}

		/// <summary>
		/// OnAfterDeserialize implementation.
		/// </summary>
		public void OnAfterDeserialize()
		{
			for (int i = 0; i < m_Keys.Count; i++)
				Add(DeserializeKey(m_Keys[i]), DeserializeValue(m_Values[i]));

			m_Keys.Clear();
			m_Values.Clear();
		}
	}

	public static class Collections
	{
		public static void PopulateWithNewInstances<T>(this IList<T> collection, int instanceCount) where T : new()
		{
			collection.Clear();
			for (int i = 0; i < instanceCount; i++) collection.Add(new T());
		}

		public static int IndexOf<TKey>(this IDictionary dictionary, TKey key)
		{
			int index = 0;
			var keys = dictionary.Keys;

			foreach (var k in keys)
			{
				if (k.Equals(key)) break;

				index++;
			}

			return index;
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

		public static int Rows<T>(this T[,] matrix) => matrix.GetLength(0);
		public static int Columns<T>(this T[,] matrix) => matrix.GetLength(1);

		public static int RemapToArrayIndex(this MatrixPosition matrixPosition, int gridDimensionSize)
		{
			return matrixPosition.R * gridDimensionSize + matrixPosition.C;
		}

		public static bool IsWithinBoundsOf(this int index, Array array)
		{
			return index >= 0 && index < array.Length;
		}

		public static bool IsWithinBoundsOf(this ushort index, Array array)
		{
			return index >= 0 && index < array.Length;
		}

		public static bool IsWithinBoundsOf<T>(this ushort index, IReadOnlyList<T> array)
		{
			return index >= 0 && index < array.Count;
		}

		public static bool IsWithinBoundsOf<T>(this int index, List<T> list)
		{
			return index >= 0 && index < list.Count;
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

		public static bool BruteForceSearch<T>(this IReadOnlyList<T> collection, Func<T, bool> filter, out T result)
		{
			if (filter == null)
			{
				result = default;
				return false;
			}

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

		public static void Filter<T>(this T[,] matrix, Func<T, bool> filter, List<T> destination, int maxCount = -1, bool shuffle = true)
		{
			destination.Clear();

			int rows = matrix.Rows();
			int cols = matrix.Columns();

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