namespace Threadlink.Core
{
    using global::Threadlink.Generated;
    using global::Threadlink.Core.NativeSubsystems.Iris;
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Scripting.LifecycleManagement;
    using UnityEngine;
    using UnityEngine.LowLevel;
    using UnityEngine.Pool;
    using FixedUpdateLoop = UnityEngine.PlayerLoop.FixedUpdate;
    using PreLateUpdateLoop = UnityEngine.PlayerLoop.PreLateUpdate;
    using UpdateLoop = UnityEngine.PlayerLoop.Update;

    /// <summary>
    /// Injects Threadlink's update events directly into Unity's PlayerLoop.
    /// No scene object or MonoBehaviour is required.
    /// </summary>
    [AutoStaticsCleanup]
    internal static partial class ThreadlinkPlayerLoop
    {
        private sealed class UpdateMarker { }
        private sealed class FixedUpdateMarker { }
        private sealed class LateUpdateMarker { }

        private static bool installed;

        public static bool Installed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => installed;
        }

        [OnEnteringPlayMode]
        private static void Reset()
        {
            // Important when Domain Reload is disabled:
            // managed statics and the installed PlayerLoop can survive independently.
            Uninstall();

            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }

        /// <summary>
        /// Installs Threadlink's native Update, FixedUpdate and LateUpdate
        /// dispatch points into Unity's current PlayerLoop.
        /// </summary>
        internal static void Install()
        {
            if (installed)
                return;

            var loop = PlayerLoop.GetCurrentPlayerLoop();

            // Remove stale Threadlink entries before installing fresh ones.
            RemoveMarkers(ref loop);

            bool updateInstalled = InsertRelative
            (
                ref loop,
                typeof(UpdateLoop.ScriptRunBehaviourUpdate),
                CreateSystem<UpdateMarker>(PublishUpdate),
                insertAfter: false
            );

            bool fixedInstalled = InsertRelative
            (
                ref loop,
                typeof(FixedUpdateLoop.ScriptRunBehaviourFixedUpdate),
                CreateSystem<FixedUpdateMarker>(PublishFixedUpdate),
                insertAfter: true
            );

            bool lateInstalled = InsertRelative
            (
                ref loop,
                typeof(PreLateUpdateLoop.ScriptRunBehaviourLateUpdate),
                CreateSystem<LateUpdateMarker>(PublishLateUpdate),
                insertAfter: true
            );

            if (!updateInstalled || !fixedInstalled || !lateInstalled)
            {
                var msg = $"Failed to install native PlayerLoop. Update: {updateInstalled}, FixedUpdate: {fixedInstalled}, LateUpdate: {lateInstalled}. The active Unity PlayerLoop does not contain the expected script update systems.";
                throw new InvalidOperationException(msg);
            }

            PlayerLoop.SetPlayerLoop(loop);
            installed = true;
        }

        /// <summary>
        /// Removes all Threadlink-owned PlayerLoop entries.
        /// Safe to call even when not installed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Uninstall()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            if (RemoveMarkers(ref loop))
                PlayerLoop.SetPlayerLoop(loop);

            installed = false;
        }

        private static PlayerLoopSystem CreateSystem<TMarker>(PlayerLoopSystem.UpdateFunction update)
        {
            return new PlayerLoopSystem { type = typeof(TMarker), updateDelegate = update };
        }

        private static void PublishUpdate()
        {
            Iris.Publish(ThreadlinkIDs.Iris.Events.OnUpdate);
        }

        private static void PublishFixedUpdate()
        {
            Iris.Publish(ThreadlinkIDs.Iris.Events.OnFixedUpdate);
        }

        private static void PublishLateUpdate()
        {
            Iris.Publish(ThreadlinkIDs.Iris.Events.OnLateUpdate);
        }

        /// <summary>
        /// Finds an existing PlayerLoop system and inserts a new system
        /// immediately before or after it.
        /// </summary>
        private static bool InsertRelative(ref PlayerLoopSystem node, Type anchorType, PlayerLoopSystem insertedSystem, bool insertAfter)
        {
            var children = node.subSystemList;

            if (children == null)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == anchorType)
                {
                    int insertIndex = insertAfter ? i + 1 : i;
                    var expanded = new PlayerLoopSystem[children.Length + 1];

                    Array.Copy
                    (
                        children,
                        0,
                        expanded,
                        0,
                        insertIndex
                    );

                    expanded[insertIndex] = insertedSystem;

                    Array.Copy
                    (
                        children,
                        insertIndex,
                        expanded,
                        insertIndex + 1,
                        children.Length - insertIndex
                    );

                    node.subSystemList = expanded;
                    return true;
                }

                var child = children[i];

                if (InsertRelative(ref child, anchorType, insertedSystem, insertAfter))
                {
                    children[i] = child;
                    node.subSystemList = children;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes any Threadlink-owned entries from the loop recursively.
        /// </summary>
        private static bool RemoveMarkers(ref PlayerLoopSystem node)
        {
            var children = node.subSystemList;

            if (children == null)
                return false;

            bool changed = false;
            using var _ = ListPool<PlayerLoopSystem>.Get(out var retained);

            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];

                if (IsThreadlinkMarker(child.type))
                {
                    changed = true;
                    continue;
                }

                if (RemoveMarkers(ref child))
                    changed = true;

                retained.Add(child);
            }

            if (changed)
                node.subSystemList = retained.ToArray();

            return changed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsThreadlinkMarker(Type type)
        {
            return type == typeof(UpdateMarker) || type == typeof(FixedUpdateMarker) || type == typeof(LateUpdateMarker);
        }
    }
}