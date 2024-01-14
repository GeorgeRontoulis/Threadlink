namespace Threadlink.Utilities.Events
{
	using UnityEngine;
	using UnityEngine.Playables;
	using UnityEngine.Timeline;

	public sealed class ScriptableEventSystemMarker : Marker, INotification
	{
		public PropertyName id => new PropertyName();
		public ScriptableEvent EventToRaise { get => eventToRaise; }

		[SerializeField] private ScriptableEvent eventToRaise = null;
	}
}