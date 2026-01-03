#if THREADLINK_TIMELINE
namespace Threadlink.Vault
{
    using DataFields;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;

    public sealed class VaultMarker : Marker, INotification, INotificationOptionProvider
    {
        private sealed class Entry
        {
            internal DataFieldIDs FieldID => fieldID;
            internal DataField DataField => dataField;

            [SerializeField] private DataFieldIDs fieldID = 0;
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