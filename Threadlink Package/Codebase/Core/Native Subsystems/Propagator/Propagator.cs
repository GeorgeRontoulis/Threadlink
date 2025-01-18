namespace Threadlink.Core.Subsystems.Propagator
{
	using Core;
	using Scribe;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;

	/// <summary>
	/// Threadlink's Event Subsystem.
	/// </summary>
	public sealed class Propagator : ThreadlinkSubsystem<Propagator>
	{
#if UNITY_EDITOR && ODIN_INSPECTOR
		[Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector]
#endif
		private Dictionary<PropagatorEvents, Delegate> EventRegistry { get; set; }

		[SerializeField] private bool displaySystemLog = false;

		public override void Discard()
		{
			if (EventRegistry != null)
			{
				EventRegistry.Clear();
				EventRegistry.TrimExcess();
				EventRegistry = null;
			}

			base.Discard();
		}

		public override void Boot()
		{
			EventRegistry = new();
			base.Boot();
		}

		#region Utility:
		public static bool TryGetListenerCount(PropagatorEvents eventID, out int listenerCount)
		{
			if (Instance == null)
			{
				listenerCount = 0;
				return false;
			}

			var registry = Instance.EventRegistry;
			bool entryIsValid = registry.ContainsKey(eventID) && registry[eventID] != null;

			listenerCount = entryIsValid ? registry[eventID].GetInvocationList().Length : -1;
			return entryIsValid;
		}

		public static bool ContainsListener<T>(PropagatorEvents eventID, T listener) where T : Delegate
		{
			if (Instance == null) return false;

			var registry = Instance.EventRegistry;

			if (registry.ContainsKey(eventID) == false || registry[eventID] == null) return false;

			return registry[eventID].GetInvocationList().Contains(listener);
		}

		public static void Discard(PropagatorEvents eventID)
		{
			var registry = Instance.EventRegistry;

			if (registry.ContainsKey(eventID))
			{
				registry[eventID] = null;
				registry.Remove(eventID);
			}
		}
		#endregion

		public static void Subscribe<T>(PropagatorEvents eventID, T listener) where T : Delegate
		{
			if (Instance == null) return;

			var registry = Instance.EventRegistry;

			if (registry.TryAdd(eventID, listener) == false)
			{
				if (registry[eventID] == null)
				{
					registry[eventID] = listener;
					if (Instance.displaySystemLog) Scribe.FromSubsystem<Propagator>("Subscribed as the only listener!").ToUnityConsole(Instance);
				}
				else if (registry[eventID].GetInvocationList().Contains(listener) == false)
				{
					registry[eventID] = Delegate.Combine(registry[eventID], listener);
					if (Instance.displaySystemLog) Scribe.FromSubsystem<Propagator>("Subscribed along with other listeners!").ToUnityConsole(Instance);
				}
				else if (Instance.displaySystemLog)
					Scribe.FromSubsystem<Propagator>("The listener is already subscribed to this event!").ToUnityConsole(Instance, Scribe.WARN);
			}
			else if (Instance.displaySystemLog) Scribe.FromSubsystem<Propagator>("Created and subscribed to this event!").ToUnityConsole(Instance);
		}

		public static void Unsubscribe<T>(PropagatorEvents eventID, T listener) where T : Delegate
		{
			if (Instance == null) return;

			var registry = Instance.EventRegistry;

			if (registry.ContainsKey(eventID))
			{
				registry[eventID] = Delegate.Remove(registry[eventID], listener);
				if (Instance.displaySystemLog) Scribe.FromSubsystem<Propagator>("Unsubscribed from this event!").ToUnityConsole(Instance);

				if (registry[eventID] == null)
				{
					Discard(eventID);
					if (Instance.displaySystemLog) Scribe.FromSubsystem<Propagator>("Unsubscribed and discarded this event!").ToUnityConsole(Instance);
				}
			}
		}

		#region Publishing:
		public static void Publish(PropagatorEvents eventID)
		{
			if (Instance == null) return;

			var registry = Instance.EventRegistry;

			if (registry.TryGetValue(eventID, out var signal))
			{
				if (signal is Action castSignal)
					castSignal.Invoke();
				else if (Instance.displaySystemLog)
					throw new InvalidCastException(Scribe.FromSubsystem<Propagator>("Invalid event type detected!").ToString());
			}
			else if (Instance.displaySystemLog)
				Scribe.FromSubsystem<Propagator>("The requested event to publish was not found!").ToUnityConsole(Instance, Scribe.WARN);
		}

		public static void Publish<Input>(PropagatorEvents eventID, Input input)
		{
			if (Instance == null) return;

			var registry = Instance.EventRegistry;

			if (registry.TryGetValue(eventID, out var signal))
			{
				if (signal is Action<Input> castSignal)
					castSignal.Invoke(input);
				else if (Instance.displaySystemLog)
					throw new InvalidCastException(Scribe.FromSubsystem<Propagator>("Invalid event type detected!").ToString());
			}
			else if (Instance.displaySystemLog)
				Scribe.FromSubsystem<Propagator>("The requested event to publish was not found!").ToUnityConsole(Instance, Scribe.WARN);
		}

		public static Output Publish<Output>(PropagatorEvents eventID)
		{
			if (Instance == null) return default;

			var registry = Instance.EventRegistry;

			if (registry.TryGetValue(eventID, out var signal))
			{
				if (signal is Func<Output> castSignal) return castSignal.Invoke();
				else if (Instance.displaySystemLog)
					throw new InvalidCastException(Scribe.FromSubsystem<Propagator>("Invalid event type detected!").ToString());
			}
			else if (Instance.displaySystemLog)
				Scribe.FromSubsystem<Propagator>("The requested event to publish was not found!").ToUnityConsole(Instance, Scribe.WARN);

			return default;
		}

		public static Output Publish<Input, Output>(PropagatorEvents eventID, Input input)
		{
			if (Instance == null) return default;

			var registry = Instance.EventRegistry;

			if (registry.TryGetValue(eventID, out var signal))
			{
				if (signal is Func<Input, Output> castSignal) return castSignal.Invoke(input);
				else if (Instance.displaySystemLog)
					throw new InvalidCastException(Scribe.FromSubsystem<Propagator>("Invalid event type detected!").ToString());
			}
			else if (Instance.displaySystemLog)
				Scribe.FromSubsystem<Propagator>("The requested event to publish was not found!").ToUnityConsole(Instance, Scribe.WARN);

			return default;
		}
		#endregion
	}
}
