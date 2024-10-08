namespace Threadlink.Utilities.Events
{
	using System;
	using Threadlink.Core;
	using UnityEngine;
	using UnityEngine.Events;
	using Utilities.UnityLogging;

	public sealed class ScriptableEventListener : LinkableBehaviour
	{
		[Serializable]
		public sealed class EventReactionPair
		{
			private const string warning = "The Event Asset could not be found! Are you missing an assignment?";

			internal UnityEvent Reaction => reaction;
			[SerializeField] private ScriptableEvent eventAsset = null;
			[SerializeField] private UnityEvent reaction = new();

#if UNITY_EDITOR
			internal void PrintMethodNames()
			{
				int count = reaction.GetPersistentEventCount();

				for (int i = 0; i < count; i++)
				{
					UnityConsole.Notify("Subscribed to ", eventAsset.name, ": ", reaction.GetPersistentMethodName(i));
				}
			}
#endif

			private void NotifyOfNullEvent() { UnityConsole.Notify(DebugNotificationType.Warning, warning); }

			internal void InvokeReaction() { reaction.Invoke(); }

			internal void Discard()
			{
				Unregister();
				eventAsset = null;
				reaction.RemoveAllListeners();
				reaction = null;
			}

			public void Register()
			{
				if (eventAsset == null) NotifyOfNullEvent(); else eventAsset.Register(this);
			}

			public void Unregister()
			{
				if (eventAsset == null) NotifyOfNullEvent(); else eventAsset.Unregister(this);
			}
		}

		public EventReactionPair[] events = new EventReactionPair[0];

		public override void Boot()
		{
			int length = events.Length;
			for (int i = 0; i < length; i++) events[i].Register();
		}

		public override void Initialize()
		{
		}

		public override VoidOutput Discard(VoidInput _ = default)
		{
			int length = events.Length;
			for (int i = 0; i < length; i++) events[i].Discard();
			return base.Discard(_);
		}
	}
}
