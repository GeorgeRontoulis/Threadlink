namespace Threadlink.Vault
{
    using Collections;
    using Collections.Extensions;
    using Core;
    using DataFields;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Threadlink's powerful and designer-friendly polymorphic data container.
    /// Your designers should use this to author their data.
    /// It includes API for actually handling that data at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Threadlink/Vault")]
    public class Vault : LinkableAsset
    {
        [SerializeField] private ReferenceTable<DataFieldIDs, DataField> dataFields = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Has(DataFieldIDs fieldID) => dataFields.ContainsKey(fieldID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryGetDataField(DataFieldIDs fieldID, out DataField result) => dataFields.TryGetValue(fieldID, out result);

        public virtual bool TryGetDataField<T>(DataFieldIDs fieldID, out DataField<T> result)
        {
            if (TryGetDataField(fieldID, out var field) && field is DataField<T> castField)
            {
                result = castField;
                return true;
            }

            result = null;
            return false;
        }

        public virtual bool TryGetDataField<T>(DataFieldIDs fieldID, out T result) where T : DataField
        {
            if (TryGetDataField(fieldID, out var field) && field is T castField)
            {
                result = castField;
                return true;
            }

            result = null;
            return false;
        }

        public virtual bool TryGet<T>(DataFieldIDs fieldID, out T value)
        {
            bool retrieved = TryGetDataField<T>(fieldID, out var field);

            value = retrieved ? field.Value : default;

            return retrieved;
        }

        public virtual bool TrySet<T>(DataFieldIDs fieldID, T value)
        {
            bool retrieved = TryGetDataField<T>(fieldID, out var field);

            if (retrieved) field.Value = value;

            return retrieved;
        }
    }
}
