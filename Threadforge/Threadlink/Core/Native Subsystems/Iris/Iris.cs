namespace Threadlink.Core.NativeSubsystems.Iris
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Threadlink's Event Subsystem.
    /// </summary>
    public static partial class Iris
    {
        private static readonly Dictionary<Events, Delegate> EventRegistry = new(1);

        /// <summary>
        /// Initialize the <see cref="Iris"/> Event Subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Observe()
        {
            EventRegistry.Clear();
        }

        #region Utility:
        public static bool TryGetListenerCount(Events eventID, out int listenerCount)
        {
            bool entryIsValid = EventRegistry.ContainsKey(eventID) && EventRegistry[eventID] != null;

            listenerCount = entryIsValid ? EventRegistry[eventID].GetInvocationList().Length : -1;
            return entryIsValid;
        }

        public static bool ContainsListener<T>(Events eventID, T listener) where T : Delegate
        {
            if (!EventRegistry.ContainsKey(eventID) || EventRegistry[eventID] == null)
                return false;

            return EventRegistry[eventID].GetInvocationList().Contains(listener);
        }

        public static void Discard(Events eventID)
        {
            if (EventRegistry.ContainsKey(eventID))
            {
                EventRegistry[eventID] = null;
                EventRegistry.Remove(eventID);
            }
        }
        #endregion

        public static void Subscribe<T>(Events eventID, T listener) where T : Delegate
        {
            if (!EventRegistry.TryAdd(eventID, listener))
            {
                if (EventRegistry[eventID] == null)
                {
                    EventRegistry[eventID] = listener;
                }
                else if (!EventRegistry[eventID].GetInvocationList().Contains(listener))
                {
                    EventRegistry[eventID] = Delegate.Combine(EventRegistry[eventID], listener);
                }
                else
                {

                }
            }
            else
            {

            }
        }

        public static void Unsubscribe<T>(Events eventID, T listener) where T : Delegate
        {
            if (EventRegistry.ContainsKey(eventID))
            {
                EventRegistry[eventID] = Delegate.Remove(EventRegistry[eventID], listener);

                if (EventRegistry[eventID] == null)
                    Discard(eventID);
            }
        }

        #region Publishing:
        public static void Publish(Events eventID)
        {
            if (EventRegistry.TryGetValue(eventID, out var signal))
            {
                if (signal is Action castSignal)
                    castSignal.Invoke();
                else
                    throw new InvalidCastException("Invalid event type detected!");
            }
            else
            {

            }
        }

        public static void Publish<Input>(Events eventID, Input input)
        {
            if (EventRegistry.TryGetValue(eventID, out var signal))
            {
                if (signal is Action<Input> castSignal)
                    castSignal.Invoke(input);
                else
                    throw new InvalidCastException("Invalid event type detected!");
            }
            else
            {

            }
        }

        public static Output Publish<Output>(Events eventID)
        {
            if (EventRegistry.TryGetValue(eventID, out var signal))
            {
                if (signal is Func<Output> castSignal)
                    return castSignal.Invoke();
                else
                    throw new InvalidCastException("Invalid event type detected!");
            }
            else
            {

            }

            return default;
        }

        public static Output Publish<Input, Output>(Events eventID, Input input)
        {
            if (EventRegistry.TryGetValue(eventID, out var signal))
            {
                if (signal is Func<Input, Output> castSignal)
                    return castSignal.Invoke(input);
                else
                    throw new InvalidCastException("Invalid event type detected!");
            }
            else
            {

            }

            return default;
        }
        #endregion
    }
}