namespace Threadlink.Utilities.Events
{
	using UnityEngine;
	using UnityEngine.Playables;

	public class ScriptableEventSystemReceiver : MonoBehaviour, INotificationReceiver
	{
		public void OnNotify(Playable origin, INotification notification, object context)
		{
			if (notification is ScriptableEventSystemMarker)
			{
				(notification as ScriptableEventSystemMarker).EventToRaise.Raise();
			}
		}
	}
}