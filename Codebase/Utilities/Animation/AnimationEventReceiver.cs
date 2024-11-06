namespace Threadlink.Utilities.Animation
{
	using UnityEngine;
	using ValidEvent = Events.ScriptableEvent;

	public sealed class AnimationEventReceiver : MonoBehaviour
	{
		/// <summary>
		/// Referenced by animation events. Do not call manually.
		/// </summary>
		/// <param name="eventObject">The scriptable event object passed by the animation event.</param>
		public void React(Object eventObject)
		{
			if (eventObject != null && eventObject is ValidEvent) (eventObject as ValidEvent).Invoke();
		}
	}
}