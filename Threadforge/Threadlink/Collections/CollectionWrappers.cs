namespace Threadlink.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    [Serializable]
    public abstract class SerializableIndexedCollection<T>
    {
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Collection[index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Collection[index] = value;
        }

        public abstract IList<T> Collection { get; set; }
    }

    [Serializable]
    public abstract class SerializableArray<T> : SerializableIndexedCollection<T>
    {
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Collection.Count;
        }
    }

    [Serializable]
    public class FieldArray<T> : SerializableArray<T>
    {
        public override IList<T> Collection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => collection;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => collection = (T[])value;
        }

        [SerializeField] private T[] collection = Array.Empty<T>();
    }

    [Serializable]
    public class RefArray<T> : SerializableArray<T>
    {
        public override IList<T> Collection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => collection;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => collection = (T[])value;
        }

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#endif
        [SerializeReference] private T[] collection = Array.Empty<T>();
    }

    [Serializable]
    public abstract class SerializableList<T> : SerializableIndexedCollection<T>
    {
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Collection.Count;
        }
    }

    [Serializable]
    public class FieldList<T> : SerializableList<T>
    {
        public override IList<T> Collection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => collection;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => collection = (List<T>)value;
        }

        [SerializeField] private List<T> collection = new(1);
    }

    [Serializable]
    public class RefList<T> : SerializableList<T>
    {
        public override IList<T> Collection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => collection;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => collection = (List<T>)value;
        }

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#endif
        [SerializeReference] private List<T> collection = new(1);
    }
}