namespace Threadlink.Vault.DataFields
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    [Serializable]
    internal abstract class DataFieldValue<T>
    {
        internal abstract T Get();
        internal abstract void Set(T input);
    }

    [Serializable]
    internal class SerializedValue<T> : DataFieldValue<T>
    {
        [SerializeField] private T field = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override T Get() => field;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Set(T input) => field = input;
    }

    [Serializable]
    internal class TransientValue<T> : DataFieldValue<T>
    {
        [field: NonSerialized]
        private T Property { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override T Get() => Property;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Set(T input) => Property = input;
    }

    [Serializable]
    public abstract class DataField : IDiscardable
    {
        public abstract void Discard();

        public abstract bool TryApplyValueTo(Vault targetVault, Vault.DataFields targetFieldID);
    }

    [Serializable]
    public class DataField<T> : DataField
    {
        public virtual T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.Get();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.value.Set(value);
                OnValueChanged?.Invoke(value);
            }
        }

        public event Action<T> OnValueChanged = null;

        [SerializeReference] internal DataFieldValue<T> value = default;

        public override void Discard()
        {
            value = default;
            OnValueChanged?.Invoke(default);
            OnValueChanged = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryApplyValueTo(Vault targetVault, Vault.DataFields targetFieldID)
        {
            return targetVault != null && targetVault.TrySet(targetFieldID, value);
        }
    }
}