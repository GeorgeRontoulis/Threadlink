namespace Threadlink.Core.NativeSubsystems.Iris
{
    using global::Threadlink.Core.NativeSubsystems.Scribe;
    using global::Threadlink.Generated;
    using global::Threadlink.Shared;
    using System.Collections.Generic;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Threadlink's Event Subsystem.
    /// </summary>
    public static partial class Iris
    {
        /// <summary>
        /// Listeners by event value. The code generator keeps an event's value when other events are removed, so values
        /// can have gaps: the registry spans the highest value, not the count.
        /// </summary>
        private static readonly object[] EventRegistry = new object[RegistrySize()];

        private static int RegistrySize()
        {
            int highest = -1;

            foreach (int value in Enum.GetValues(typeof(ThreadlinkIDs.Iris.Events)))
                highest = Math.Max(highest, value);

            return highest + 1;
        }

        [OnExitingPlayMode]
        private static void Reset()
        {
            int length = EventRegistry.Length;

            for (int i = 0; i < length; i++)
                (EventRegistry[i] as IClearable)?.Clear();
        }

        #region Monitoring:
        [Monitored("Iris", Description = "The event bus: subscribe, unsubscribe and publish by generated event ID.", Order = -1000)]
        private static void Report(MonitorReport report)
        {
            int total = 0, withListeners = 0;
            int length = EventRegistry.Length;

            for (int i = 0; i < length; i++)
            {
                if (EventRegistry[i] is IDelegateList list && list.Count > 0)
                {
                    total += list.Count;
                    withListeners++;
                }
            }

            report.Summary(total + (total == 1 ? " listener" : " listeners"));

            if (!report.Detailed)
                return;

            var events = (ThreadlinkIDs.Iris.Events[])Enum.GetValues(typeof(ThreadlinkIDs.Iris.Events));

            report.Section("Events")
                .Field("Declared", events.Length)
                .Field("With listeners", withListeners)
                .Field("Listeners", total);

            if (withListeners == 0)
                return;

            var counted = new List<(string Name, int Count)>(withListeners);

            foreach (var eventID in events)
            {
                if (TryGetListenerCount(eventID, out int count) && count > 0)
                    counted.Add((eventID.ToString(), count));
            }

            counted.Sort((a, b) => b.Count.CompareTo(a.Count));
            report.Section("Listeners by event");

            foreach (var (name, count) in counted)
                report.Field(name, count);
        }
        #endregion

        #region Utility:
        public static bool TryGetListenerCount(ThreadlinkIDs.Iris.Events eventID, out int listenerCount)
        {
            var index = (int)eventID;

            if (EventRegistry[index] is IDelegateList list)
            {
                listenerCount = list.Count;
                return true;
            }

            listenerCount = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsListener<T>(ThreadlinkIDs.Iris.Events eventID, T listener) where T : Delegate
        {
            return (EventRegistry[(int)eventID] as DelegateList<T>)?.Contains(listener) ?? false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear(ThreadlinkIDs.Iris.Events eventID)
        {
            (EventRegistry[(int)eventID] as IClearable)?.Clear();
        }
        #endregion

        public static void Subscribe<T>(ThreadlinkIDs.Iris.Events eventID, T listener) where T : Delegate
        {
            ref var slot = ref EventRegistry[(int)eventID];

            slot ??= new DelegateList<T>();

            if (slot is not DelegateList<T> list)
            {
                slot.Send($"Type mismatch on Subscribe for event '{eventID}'. Expected DelegateList<{typeof(T).Name}>.").ToUnityConsole(DebugType.Error);
                return;
            }

            if (!list.Contains(listener))
                list.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unsubscribe<T>(ThreadlinkIDs.Iris.Events eventID, T listener) where T : Delegate
        {
            (EventRegistry[(int)eventID] as DelegateList<T>)?.Remove(listener);
        }

        #region Publishing:
        public static void Publish(ThreadlinkIDs.Iris.Events eventID)
        {
            if (EventRegistry[(int)eventID] is not DelegateList<Action> list)
                return;

            int bound = list.BeginDispatch();
            try
            {
                for (int i = bound - 1; i >= 0; i--) list.slots[i]?.Invoke();
            }
            finally { list.EndDispatch(); }
        }

        public static void Publish<Input>(ThreadlinkIDs.Iris.Events eventID, Input input)
        {
            if (EventRegistry[(int)eventID] is not DelegateList<Action<Input>> list)
                return;

            int bound = list.BeginDispatch();
            try
            {
                for (int i = bound - 1; i >= 0; i--) list.slots[i]?.Invoke(input);
            }
            finally { list.EndDispatch(); }
        }

        public static Output Publish<Output>(ThreadlinkIDs.Iris.Events eventID)
        {
            if (EventRegistry[(int)eventID] is not DelegateList<Func<Output>> list)
                return default;

            return list.Count switch
            {
                0 => default,
                1 => list.slots[0].Invoke(),
                _ => throw OnlyOneListenerException(eventID, list)
            };
        }

        public static Output Publish<Input, Output>(ThreadlinkIDs.Iris.Events eventID, Input input)
        {
            if (EventRegistry[(int)eventID] is not DelegateList<Func<Input, Output>> list)
                return default;

            return list.Count switch
            {
                0 => default,
                1 => list.slots[0].Invoke(input),
                _ => throw OnlyOneListenerException(eventID, list)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException OnlyOneListenerException(ThreadlinkIDs.Iris.Events eventID, IDelegateList list)
        {
            return new InvalidOperationException($"[Iris] Func event '{eventID}' expects exactly 1 listener but found {list.Count}.");
        }
        #endregion
    }
}
