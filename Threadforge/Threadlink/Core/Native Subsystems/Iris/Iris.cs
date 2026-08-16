namespace Threadlink.Core.NativeSubsystems.Iris
{
    using global::Threadlink.Core.NativeSubsystems.Scribe;
    using global::Threadlink.Generated;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Threadlink's Event Subsystem.
    /// </summary>
    public static partial class Iris
    {
        private static readonly object[] EventRegistry = new object[Enum.GetValues(typeof(ThreadlinkIDs.Iris.Events)).Length];

        [OnExitingPlayMode]
        private static void Reset()
        {
            int length = EventRegistry.Length;

            for (int i = 0; i < length; i++)
                (EventRegistry[i] as IClearable)?.Clear();
        }

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

            var slots = list.slots;

            for (int i = list.Count - 1; i >= 0; i--)
                slots[i].Invoke();
        }

        public static void Publish<Input>(ThreadlinkIDs.Iris.Events eventID, Input input)
        {
            if (EventRegistry[(int)eventID] is not DelegateList<Action<Input>> list)
                return;

            var slots = list.slots;

            for (int i = list.Count - 1; i >= 0; i--)
                slots[i].Invoke(input);
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
