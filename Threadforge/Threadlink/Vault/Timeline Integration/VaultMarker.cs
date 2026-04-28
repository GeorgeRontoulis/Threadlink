#if THREADLINK_TIMELINE
namespace Threadlink.Vault
{
    using Shared;
    using System;
    using System.Collections.Generic;
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

        public PropertyName id => ID;
        public NotificationFlags flags => options;

        [SerializeField] private NotificationFlags options = 0;
        [SerializeField] private List<Entry> configuration = new();

        public bool TryApplyConfigTo(Vault vault)
        {
            if (vault == null) return false;

            foreach (var config in configuration)
            {
                var field = config.DataField;

                if (field == null) continue;

                field.TryApplyValueTo(vault, config.FieldID);
            }

            return true;
        }
    }
}
#endif