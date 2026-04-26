namespace Threadlink.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    [Serializable]
    public class FieldHashMap<TKey, TValue> : ThreadlinkHashMap<TKey, TValue>
    {
        [SerializeField] private TValue[] values = Array.Empty<TValue>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ref TValue[] GetValuesRef() => ref values;
    }

    [Serializable]
    public class RefHashMap<TKey, TValue> : ThreadlinkHashMap<TKey, TValue>
    {
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
        [SerializeField] private TKey[] keys = Array.Empty<TKey>();
        [SerializeField] private int count = 0;

        private int[] buckets;
        private int[] next;

        protected abstract ref TValue[] GetValuesRef();

        #region Serialization Callbacks:
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (keys == null || count == 0)
                return;

            int capacity = keys.Length;
            buckets = new int[capacity];
            Array.Fill(buckets, -1);
            next = new int[capacity];

            var comparer = EqualityComparer<TKey>.Default;

            for (int i = 0; i < count; i++)
            {
                // Mask the hash to ensure it's positive for bucket modulo math
                int hashCode = comparer.GetHashCode(keys[i]) & 0x7FFFFFFF;
                int targetBucket = hashCode % capacity;

                next[i] = buckets[targetBucket];
                buckets[targetBucket] = i;
            }
        }
        #endregion

        #region Editor-Only:
#if UNITY_EDITOR
        public bool EditorOnly_TryAdd(TKey key, TValue value)
        {
            if (ContainsKey(key))
                return false;

            int length = keys.Length;
            if (count >= length)
            {
                int newCapacity = length == 0 ? 4 : length + length;
                Array.Resize(ref keys, newCapacity);
                Array.Resize(ref GetValuesRef(), newCapacity);
            }

            keys[count] = key;
            GetValuesRef()[count] = value;
            count++;

            OnAfterDeserialize();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_Add(TKey key, TValue value)
        {
            if (!EditorOnly_TryAdd(key, value))
                throw new ArgumentException($"An item with the same key has already been added: {key}");
        }

        public bool EditorOnly_Remove(TKey key)
        {
            if (buckets == null)
                return false;

            var comparer = EqualityComparer<TKey>.Default;
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int targetBucket = hashCode % buckets.Length;
            int targetIndex = buckets[targetBucket];

            int indexToRemove = -1;
            for (int i = targetIndex; i >= 0; i = next[i])
            {
                if (comparer.Equals(keys[i], key))
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove == -1)
                return false;

            int countMinusOne = count - 1;

            for (int i = indexToRemove; i < countMinusOne; i++)
            {
                keys[i] = keys[i + 1];
                GetValuesRef()[i] = GetValuesRef()[i + 1];
            }

            count--;

            keys[count] = default;
            GetValuesRef()[count] = default;

            OnAfterDeserialize();
            return true;
        }
#endif
        #endregion

        #region Public API:
        public bool ContainsKey(TKey key)
        {
            if (buckets != null)
            {
                var comparer = EqualityComparer<TKey>.Default;
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                int targetBucket = hashCode % buckets.Length;
                int targetIndex = buckets[targetBucket];

                for (int i = targetIndex; i >= 0; i = next[i])
                {
                    if (comparer.Equals(keys[i], key))
                        return true;
                }
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (buckets != null)
            {
                var comparer = EqualityComparer<TKey>.Default;
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                int targetBucket = hashCode % buckets.Length;
                int targetIndex = buckets[targetBucket];

                for (int i = targetIndex; i >= 0; i = next[i])
                {
                    if (comparer.Equals(keys[i], key))
                    {
                        value = GetValuesRef()[i];
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }
        #endregion
    }
}