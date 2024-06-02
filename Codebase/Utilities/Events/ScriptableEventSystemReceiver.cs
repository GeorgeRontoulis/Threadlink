namespace Threadlink.Utilities.Events
{
	using UnityEngine;
	using UnityEngine.Playables;
	using ValidMarker = ScriptableEventSystemMarker;

	public class ScriptableEventSystemReceiver : MonoBehaviour, INotificationReceiver
	{
		public void OnNotify(Playable origin, INotification notification, object context)
		{
			if (notification is ValidMarker) (notification as ValidMarker).EventToRaise.Raise();
		}
	}
}