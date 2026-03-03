namespace Threadlink.Collections
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    #region Entry Classes:
    [Serializable]
    public sealed class FieldEntry<K, V> : Entry<K, Field<V>>
    {
        public V Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                value ??= new();
                return value.FieldValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.value ??= new();
                this.value.FieldValue = value;
            }
        }
    }

    [Serializable]
    public sealed class ReferenceEntry<K, V> : Entry<K, Reference<V>>
    {
        public V Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                value ??= new();
                return value.ReferenceValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.value ??= new();
                this.value.ReferenceValue = value;
            }
        }
    }

    [Serializable]
    public abstract class Entry<K, V> where V : SerializableValue
    {
        public K Key
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => key;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => key = value;
        }

#if ODIN_INSPECTOR
        [HideLabel, HorizontalGroup]
#endif
        [SerializeField] private K key = default;

#if ODIN_INSPECTOR
        [HideLabel, HorizontalGroup]
#endif
        [SerializeField] protected V value = default;
    }
    #endregion

    #region Value Classes:
    [Serializable]
    public abstract class SerializableValue { }

    [Serializable]
    public sealed class Field<T> : SerializableValue
    {
        internal T FieldValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => fieldValue;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => fieldValue = value;
        }

#if ODIN_INSPECTOR
        [HideLabel]
#endif
        [SerializeField] private T fieldValue = default;
    }

    [Serializable]
    public sealed class Reference<T> : SerializableValue
    {
        internal T ReferenceValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => referenceValue;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => referenceValue = value;
        }

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#else
        [HideLabel]
#endif
        [SerializeReference] private T referenceValue = default;
    }
    #endregion
}
