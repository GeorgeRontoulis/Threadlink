#if THREADLINK_TIMELINE
namespace Threadlink.Vault
{
    using System;
    using UnityEngine;
    using UnityEngine.Playables;

    public sealed class VaultReceiver : MonoBehaviour, INotificationReceiver
    {
        public event Action<PlayableDirector, VaultMarker> OnMarkerReceived = null;

        public void ClearCallback() => OnMarkerReceived = null;

        public void OnNotify(Playable origin, INotification notification, object _)
        {
            if (notification is VaultMarker marker && origin.GetGraph().GetResolver() is PlayableDirector director)
            {
                OnMarkerReceived?.Invoke(director, marker);
            }
        }
    }
}
#endif