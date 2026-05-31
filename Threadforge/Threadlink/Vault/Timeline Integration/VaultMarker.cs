#if THREADLINK_TIMELINE
namespace Threadlink.Vault
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;

    public sealed class VaultMarker : Marker, INotification, INotificationOptionProvider
    {
        [Serializable]
        private sealed class Entry
        {
            internal ThreadlinkIDs.Vault.Fields FieldID => fieldID;
            internal DataField DataField => dataField;

            [SerializeField] private ThreadlinkIDs.Vault.Fields fieldID = 0;
            [SerializeReference] private DataField dataField = null;
        }

        static readonly PropertyName ID = new("VaultMarker");

        public PropertyName id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ID;
        }

        public NotificationFlags flags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => options;
        }

        [SerializeField] private NotificationFlags options = 0;
        [SerializeField] private List<Entry> configuration = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryApplyConfigTo(Vault vault)
        {
            if (vault == null)
                return false;

            int count = configuration.Count;
            for (int i = 0; i < count; i++)
            {
                var config = configuration[i];
                var field = config.DataField;

                if (field == null) continue;

                if (!field.TryApplyValueTo(vault, config.FieldID))
                    Debug.LogError($"COULD NOT APPLY VALUE TO {vault.name}! KEY: {config.FieldID}, VALUE: {config.DataField.GetType().Name}");
            }

            return true;
        }
    }
}
#endif