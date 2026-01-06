namespace Threadlink.Vault.DataFields
{
    using Shared;
    using System;
    using UnityEngine;

    [Serializable]
    public abstract class DataField : IDiscardable
    {
        public virtual void Discard() { }

        public abstract bool TryApplyValueTo(Vault targetVault, Vault.DataFields targetFieldID);
    }

    [Serializable]
    public class DataField<T> : DataField
    {
        public virtual T Value
        {
            get => value;
            set
            {
                this.value = value;
                OnValueChanged?.Invoke(value);
            }
        }

        public event Action<T> OnValueChanged = null;

        [SerializeField] protected T value = default;

        public override void Discard()
        {
            value = default;
            OnValueChanged?.Invoke(default);
            OnValueChanged = null;
        }

        public override bool TryApplyValueTo(Vault targetVault, Vault.DataFields targetFieldID)
        {
            return targetVault != null && targetVault.TrySet(targetFieldID, value);
        }
    }
}