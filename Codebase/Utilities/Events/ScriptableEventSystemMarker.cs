#if THREADLINK_INTEGRATIONS_TIMELINE
namespace Threadlink.Utilities.Events
{
	using UnityEngine;
	using UnityEngine.Playables;
	using UnityEngine.Timeline;

	public sealed class ScriptableEventSystemMarker : Marker, INotification
	{
		public PropertyName id => new();
		public ScriptableEvent EventToRaise => eventToRaise;

		[SerializeField] private ScriptableEvent eventToRaise = null;
	}
}
#endif