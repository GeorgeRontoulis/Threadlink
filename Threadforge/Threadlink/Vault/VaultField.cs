namespace Threadlink.Vault
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Shared;
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
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HideLabel]
#endif
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
        public abstract bool TryApplyValueTo(Vault targetVault, ThreadlinkIDs.Vault.Fields targetFieldID);
        public abstract void Discard();
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

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#else
        [Sirenix.OdinInspector.HideLabel]
#endif
        [SerializeReference] internal DataFieldValue<T> value = default;

        public override void Discard()
        {
            value = default;
            OnValueChanged?.Invoke(default);
            OnValueChanged = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryApplyValueTo(Vault targetVault, ThreadlinkIDs.Vault.Fields targetFieldID)
        {
            return targetVault != null && targetVault.TrySet(targetFieldID, value);
        }
    }
}