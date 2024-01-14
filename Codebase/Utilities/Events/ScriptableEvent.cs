namespace Threadlink.Utilities.Events
{
	using Sirenix.OdinInspector;
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.UnityLogging;
	using Pair = ScriptableEventListener.EventReactionPair;

	[CreateAssetMenu(menuName = "Threadlink/Event Utilities/Scriptable Event")]
	public sealed class ScriptableEvent : ScriptableObject, IIdentifiable
	{
		public string LinkID => name;

		[NonSerialized] private List<Pair> subscribers = new List<Pair>();

		internal void Register(Pair pair) { subscribers.Add(pair); }
		internal void Unregister(Pair pair) { subscribers.Remove(pair); }
		public void Raise() { for (int i = subscribers.Count - 1; i >= 0; i--) subscribers[i].InvokeReaction(); }

		public void UnregisterAll()
		{
			subscribers.Clear();
			subscribers.TrimExcess();
		}

#if UNITY_EDITOR
		[Button]
		private void PrintStatus()
		{
			for (int i = subscribers.Count - 1; i >= 0; i--)
			{
				UnityConsole.Notify("Subscriber ", i, "has the following methods registered:");
				subscribers[i].PrintMethodNames();
				UnityConsole.Notify("-------------------------------------------------------");
			}
		}
#endif
	}
}
