namespace Threadlink.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Utilities.Collections;
    using UnityEngine;

    [Serializable]
    public class FieldHashMap<TKey, TValue> : ThreadlinkHashMap<TKey, TValue>
    {
        public override ReadOnlySpan<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(values, 0, count);
        }

        public override ReadOnlyMemory<TValue> ValueMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(values, 0, count);
        }

        [SerializeField] private TValue[] values = Array.Empty<TValue>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ref TValue[] GetValuesRef() => ref values;
    }

    [Serializable]
    public class RefHashMap<TKey, TValue> : ThreadlinkHashMap<TKey, TValue>
    {
        public override ReadOnlySpan<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(values, 0, count);
        }

        public override ReadOnlyMemory<TValue> ValueMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(values, 0, count);
        }

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#endif
        [SerializeReference] private TValue[] values = Array.Empty<TValue>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ref TValue[] GetValuesRef() => ref values;
    }

    [Serializable]
    public abstract class ThreadlinkHashMap<TKey, TValue> : ISerializationCallbackReceiver
    {
        public delegate void Action(TKey key, ref TValue value);

        public abstract ReadOnlySpan<TValue> Values { get; }
        public abstract ReadOnlyMemory<TValue> ValueMemory { get; }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        public ReadOnlySpan<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(keys, 0, count);
        }

        public ReadOnlyMemory<TKey> KeyMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(keys, 0, count);
        }

        private static EqualityComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => EqualityComparer<TKey>.Default;
        }

        [SerializeField] private TKey[] keys = Array.Empty<TKey>();
        [SerializeField] protected int count = 0;

        private int[] buckets = Array.Empty<int>();
        private int[] next = Array.Empty<int>();

        protected abstract ref TValue[] GetValuesRef();

        #region Serialization Callbacks:
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (keys == null || keys.Length == 0)
            {
                buckets = Array.Empty<int>();
                next = Array.Empty<int>();
                return;
            }

            int capacity = keys.Length;

            // Minor optimization: Only allocate if the size changed
            if (buckets == null || buckets.Length != capacity)
                buckets = new int[capacity];

            Array.Fill(buckets, -1);

            if (next == null || next.Length != capacity)
                next = new int[capacity];

            // Safe exit AFTER clearing the buckets
            if (count == 0) return;

            var comparer = EqualityComparer<TKey>.Default;

            for (int i = 0; i < count; i++)
            {
                int hashCode = comparer.GetHashCode(keys[i]) & 0x7FFFFFFF;
                int targetBucket = hashCode % capacity;

                next[i] = buckets[targetBucket];
                buckets[targetBucket] = i;
            }
        }
        #endregion

        #region Editor-Only:
#if UNITY_EDITOR
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int index = KeyToTrueIndex(key);
                if (index >= 0)
                    return GetValuesRef()[index];

                Debug.LogException(new KeyNotFoundException($"The given key '{key}' is not present in the map!"));
                return default;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                int index = KeyToTrueIndex(key);
                ref var values = ref GetValuesRef();

                if (index.IsWithinBoundsOf(values))
                    values[index] = value;
                else if (!ContainsKey(key))
                    EditorOnly_Add(key, value);
            }
        }

        public bool EditorOnly_TryAdd(TKey key, TValue value)
        {
            if (ContainsKey(key))
                return false;

            int length = keys.Length;
            ref var values = ref GetValuesRef();

            if (count >= length)
            {
                int newCapacity = length == 0 ? 4 : length + length;
                Array.Resize(ref keys, newCapacity);
                Array.Resize(ref values, newCapacity);
            }

            keys[count] = key;
            values[count] = value;
            count++;

            OnAfterDeserialize();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_Add(TKey key, TValue value)
        {
            if (!EditorOnly_TryAdd(key, value))
                Debug.LogException(new ArgumentException($"An item with the same key has already been added: {key}"));
        }

        public bool EditorOnly_Remove(TKey key)
        {
            if (buckets == null)
                return false;

            int targetBucket = KeyToBucketIndex(key);
            int indexToRemove = -1;

            for (int i = targetBucket; i >= 0; i = next[i])
            {
                if (Comparer.Equals(keys[i], key))
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove == -1)
                return false;

            int countMinusOne = count - 1;
            ref var values = ref GetValuesRef();

            for (int i = indexToRemove; i < countMinusOne; i++)
            {
                keys[i] = keys[i + 1];
                values[i] = values[i + 1];
            }

            --count;

            keys[count] = default;
            values[count] = default;

            OnAfterDeserialize();
            return true;
        }
#endif
        #endregion

        #region Private API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int KeyToBucketIndex(TKey key)
        {
            int length = buckets.Length;
            if (length == 0) return -1; // Protection against DivideByZero

            int hashCode = Comparer.GetHashCode(key) & 0x7FFFFFFF;
            int targetBucket = hashCode % length;
            return buckets[targetBucket];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int KeyToTrueIndex(TKey key)
        {
            if (buckets != null && buckets.Length > 0)
            {
                int targetEntry = KeyToBucketIndex(key);

                // Traverse the next-pointer chain for this specific bucket
                for (int i = targetEntry; i >= 0; i = next[i])
                {
                    if (Comparer.Equals(keys[i], key))
                        return i;
                }
            }

            return -1;
        }
        #endregion

        #region Public API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void For(Action action)
        {
            if (action == null)
                return;

            ref var values = ref GetValuesRef();

            for (int i = 0; i < count; i++)
                action.Invoke(keys[i], ref values[i]);
        }

        public bool ContainsKey(TKey key)
        {
            if (buckets != null)
            {
                int targetButon = KeyToBucketIndex(key);

                for (int i = targetButon; i >= 0; i = next[i])
                {
                    if (Comparer.Equals(keys[i], key))
                        return true;
                }
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (buckets != null)
            {
                int targetBucket = KeyToBucketIndex(key);
                ref var values = ref GetValuesRef();

                for (int i = targetBucket; i >= 0; i = next[i])
                {
                    if (Comparer.Equals(keys[i], key))
                    {
                        value = values[i];
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        public void Clear()
        {
            if (count > 0)
            {
                // Clear the active elements to release memory and GC references
                Array.Clear(keys, 0, count);
                Array.Clear(GetValuesRef(), 0, count);

                count = 0;
            }

            if (buckets != null)
            {
                // Reset buckets to -1 so all hash lookups immediately terminate
                Array.Fill(buckets, -1);

                // Clear the next pointer chains
                Array.Clear(next, 0, next.Length);
            }
        }
        #endregion
    }
}