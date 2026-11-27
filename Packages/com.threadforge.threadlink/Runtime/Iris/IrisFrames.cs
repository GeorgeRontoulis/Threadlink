namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;
    using UnityEngine.LowLevel;

    /// <summary>Published on Iris every Update, after MonoBehaviour Update.</summary>
    public readonly struct UpdateFrame
    {
        /// <summary>Seconds since the previous frame (<see cref="Time.deltaTime"/>).</summary>
        public readonly float DeltaTime;

        internal UpdateFrame(float deltaTime) => DeltaTime = deltaTime;
    }

    /// <summary>Published on Iris every FixedUpdate, after MonoBehaviour FixedUpdate.</summary>
    public readonly struct FixedFrame
    {
        /// <summary>Seconds per fixed step (<see cref="Time.fixedDeltaTime"/>).</summary>
        public readonly float DeltaTime;

        internal FixedFrame(float deltaTime) => DeltaTime = deltaTime;
    }

    /// <summary>Published on Iris every LateUpdate, after MonoBehaviour LateUpdate.</summary>
    public readonly struct LateFrame
    {
        /// <summary>Seconds since the previous frame (<see cref="Time.deltaTime"/>).</summary>
        public readonly float DeltaTime;

        internal LateFrame(float deltaTime) => DeltaTime = deltaTime;
    }

    /// <summary>Inserts Iris's frame markers into Unity's PlayerLoop while Iris is linked.</summary>
    internal static class IrisFrames
    {
        private sealed class UpdateMarker { }

        private sealed class FixedMarker { }

        private sealed class LateMarker { }

        internal static bool Installed { get; private set; }

        /// <summary>Installs exactly one set of markers into the current loop. Requires Unity's main thread.</summary>
        internal static void Install()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop);
            if (
                !Insert(
                    ref loop,
                    typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                    new PlayerLoopSystem
                    {
                        type = typeof(UpdateMarker),
                        updateDelegate = DriveUpdate,
                    }
                )
                || !Insert(
                    ref loop,
                    typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate),
                    new PlayerLoopSystem
                    {
                        type = typeof(FixedMarker),
                        updateDelegate = DriveFixed,
                    }
                )
                || !Insert(
                    ref loop,
                    typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
                    new PlayerLoopSystem
                    {
                        type = typeof(LateMarker),
                        updateDelegate = DriveLate,
                    }
                )
            )
                throw new InvalidOperationException(
                    "Unity frame anchors are absent from the active PlayerLoop."
                );
            PlayerLoop.SetPlayerLoop(loop);
            Installed = true;
        }

        /// <summary>Removes only this adapter's markers; preserves all other current loop changes.</summary>
        internal static void Uninstall()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (Remove(ref loop))
                PlayerLoop.SetPlayerLoop(loop);
            Installed = false;
        }

        private static void DriveUpdate()
        {
            if (!Iris.TryGetSingleton(out var iris))
                return;
            iris.Bus.DrainPosted();
            iris.Bus.PublishIsolated(new UpdateFrame(Time.deltaTime));
        }

        private static void DriveFixed()
        {
            if (Iris.TryGetSingleton(out var iris))
                iris.Bus.PublishIsolated(new FixedFrame(Time.fixedDeltaTime));
        }

        private static void DriveLate()
        {
            if (Iris.TryGetSingleton(out var iris))
                iris.Bus.PublishIsolated(new LateFrame(Time.deltaTime));
        }

        private static bool Insert(ref PlayerLoopSystem node, Type anchor, PlayerLoopSystem marker)
        {
            var children = node.subSystemList;
            if (children == null)
                return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == anchor)
                {
                    var expanded = new PlayerLoopSystem[children.Length + 1];
                    Array.Copy(children, 0, expanded, 0, i + 1);
                    expanded[i + 1] = marker;
                    Array.Copy(children, i + 1, expanded, i + 2, children.Length - i - 1);
                    node.subSystemList = expanded;
                    return true;
                }
                if (Insert(ref children[i], anchor, marker))
                    return true;
            }
            return false;
        }

        private static bool Remove(ref PlayerLoopSystem node)
        {
            var children = node.subSystemList;
            if (children == null)
                return false;
            bool changed = false;
            int count = 0;
            // Copy before mutation: Unity may retain arrays returned by GetCurrentPlayerLoop.
            var kept = new PlayerLoopSystem[children.Length];
            foreach (var child in children)
            {
                if (
                    child.type == typeof(UpdateMarker)
                    || child.type == typeof(FixedMarker)
                    || child.type == typeof(LateMarker)
                )
                {
                    changed = true;
                    continue;
                }
                var entry = child;
                changed |= Remove(ref entry);
                kept[count++] = entry;
            }
            if (changed)
            {
                Array.Resize(ref kept, count);
                node.subSystemList = kept;
            }
            return changed;
        }
    }
}
