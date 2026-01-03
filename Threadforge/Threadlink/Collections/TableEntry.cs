namespace Threadlink.Collections
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    using System;
    using UnityEngine;

    #region Entry Classes:
    [Serializable]
    public sealed class FieldEntry<K, V> : Entry<K, Field<V>>
    {
        public V Value => value.FieldValue;
    }

    [Serializable]
    public sealed class ReferenceEntry<K, V> : Entry<K, Reference<V>>
    {
        public V Value => value.ReferenceValue;
    }

    [Serializable]
    public abstract class Entry<K, V> where V : SerializableValue
    {
        public K Key => key;

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
        internal T FieldValue => fieldValue;

#if ODIN_INSPECTOR
        [HideLabel]
#endif
        [SerializeField] private T fieldValue = default;
    }

    [Serializable]
    public sealed class Reference<T> : SerializableValue
    {
        internal T ReferenceValue => referenceValue;

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#else
        [HideLabel]
#endif
        [SerializeReference] private T referenceValue = default;
    }
    #endregion
}
