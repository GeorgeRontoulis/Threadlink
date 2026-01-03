namespace Threadlink.Collections
{
    using Extensions;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Collections;

    [Serializable]
    public class ReferenceTable<K, V> : ThreadlinkTable<K, Reference<V>, ReferenceEntry<K, V>>
    {
        public V this[K key]
        {
            get
            {
                int index = this.IndexOf(key);

                if (index.IsWithinBoundsOf(entries))
                    return entries[index].Value;

                return default;
            }
        }

        public IEnumerable<K> Values
        {
            get
            {
                int length = entries.Length;

                for (int i = 0; i < length; i++)
                    yield return entries[i].Key;
            }
        }

        internal override ReferenceEntry<K, V>[] Entries => entries;

        [SerializeField] private ReferenceEntry<K, V>[] entries = new ReferenceEntry<K, V>[0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override bool ContainsKey(K key) => this.IndexOf(key).IsWithinBoundsOf(entries);
    }

    [Serializable]
    public class FieldTable<K, V> : ThreadlinkTable<K, Field<V>, FieldEntry<K, V>>
    {
        public V this[K key]
        {
            get
            {
                int index = this.IndexOf(key);

                if (index.IsWithinBoundsOf(entries))
                    return entries[index].Value;

                return default;
            }
        }

        public IEnumerable<K> Values
        {
            get
            {
                int length = entries.Length;

                for (int i = 0; i < length; i++)
                    yield return entries[i].Key;
            }
        }

        internal override FieldEntry<K, V>[] Entries => entries;

        [SerializeField] private FieldEntry<K, V>[] entries = new FieldEntry<K, V>[0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override bool ContainsKey(K key) => this.IndexOf(key).IsWithinBoundsOf(entries);
    }

    [Serializable]
    public abstract class ThreadlinkTable<K, V, E> : IEnumerable<E>
    where E : Entry<K, V>
    where V : SerializableValue
    {
        internal EqualityComparer<K> KeyComparer => keyComparer;

        protected EqualityComparer<K> keyComparer = EqualityComparer<K>.Default;

        public int Count => Entries.Length;
        internal abstract E[] Entries { get; }
        internal abstract bool ContainsKey(K key);

        public IEnumerable<K> Keys
        {
            get
            {
                int length = Entries.Length;

                for (int i = 0; i < length; i++)
                    yield return Entries[i].Key;
            }
        }

        public IEnumerator<E> GetEnumerator()
        {
            var entries = Entries;
            int count = entries.Length;

            for (int i = 0; i < count; i++)
                yield return entries[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}