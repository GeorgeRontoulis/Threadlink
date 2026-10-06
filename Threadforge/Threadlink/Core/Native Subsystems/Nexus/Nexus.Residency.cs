namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Core;
    using Cysharp.Threading.Tasks;
    using Generated;
    using Initium;
    using Iris;
    using Scribe;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Unity.Scripting.LifecycleManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        internal enum SceneState
        {
            Loading,
            Resident,
            Unloading,
            UnloadFailed,
        }

        /// <summary>One scene's residency: its state, its holds, and the operation in flight.</summary>
        internal sealed class SceneRecord
        {
            public ISceneEntry Entry { get; set; }
            public ThreadlinkIDs.Addressables.Scenes Pointer { get; set; }
            public SceneState State { get; set; }
            public int Holds { get; set; }
            public Scene Scene { get; set; }
            public bool Discarded { get; set; }

            /// <summary>Completes once the load does, true if the scene is resident: every holder waiting on it shares it.</summary>
            public UniTaskCompletionSource<bool> Loaded { get; } = new();

            /// <summary>Completes once the unload does. Set when unloading starts.</summary>
            public UniTaskCompletionSource Unloaded { get; set; }

            /// <summary>A delayed unload, cancelled by a new hold.</summary>
            public CancellationTokenSource PendingUnload { get; set; }
        }

        /// <summary>
        /// Every scene Nexus holds or is loading or unloading, by pointer. Emptied by Threadlink's shutdown
        /// (<see cref="ReleaseAll"/>), and again when Play Mode exits.
        /// </summary>
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<ThreadlinkIDs.Addressables.Scenes, SceneRecord> Records = new(8);

        #region Residency:
        /// <summary>
        /// Hold <paramref name="entry"/>'s scene resident, loading it if needed: a scene is loaded while anything holds
        /// it, and unloads once its last hold is released (<see cref="SceneHold.Release()"/>).
        /// <list type="bullet">
        /// <item>The scene loads additively with the entry's <see cref="ISceneEntry.PhysicsMode"/>, its objects boot, and
        /// <see cref="ISceneEntry.OnFinishedLoadingAsync"/> runs before any holder gets it; then
        /// <see cref="ThreadlinkIDs.Iris.Events.OnSceneResident"/> is published.</item>
        /// <item>Holders arriving during the load share it. A hold during an unload waits for it, then loads the scene
        /// again. A hold during a delayed unload cancels it.</item>
        /// <item>Residency changes nothing the player sees or hears: <see cref="PresentAsync"/> does.</item>
        /// </list>
        /// </summary>
        /// <returns>The hold, or null if the scene could not be loaded.</returns>
        public static async UniTask<SceneHold> HoldAsync(ISceneEntry entry)
        {
            if (entry == null)
                return null;

            while (true)
            {
                if (!Records.TryGetValue(entry.ScenePointer, out var record))
                {
                    record = new SceneRecord { Entry = entry, Pointer = entry.ScenePointer, State = SceneState.Loading };
                    Records[record.Pointer] = record;
                    LoadAsync(record).Forget();
                }
                else if (record.State is SceneState.Unloading)
                {
                    // The record is gone once the unload completes; the next pass loads the scene anew.
                    await record.Unloaded.Task;
                    continue;
                }
                else if (record.State is SceneState.UnloadFailed)
                {
                    await UnloadAsync(record);
                    if (IsCurrent(record) && record.State is SceneState.UnloadFailed) return null;
                    continue;
                }

                record.Holds++;
                CancelPendingUnload(record);

                if (record.State is SceneState.Loading && !await record.Loaded.Task)
                    return null;

                return new SceneHold(record);
            }
        }

        /// <summary>Whether <paramref name="entry"/>'s scene is resident: loaded and booted, and not unloading.</summary>
        public static bool IsResident(ISceneEntry entry) => entry != null && TryGetResidentScene(entry.ScenePointer, out _);

        /// <summary>The resident scene of <paramref name="entry"/>: loaded and booted, and not unloading.</summary>
        public static bool TryGetResidentScene(ISceneEntry entry, out Scene scene)
        {
            scene = default;
            return entry != null && TryGetResidentScene(entry.ScenePointer, out scene);
        }

        /// <inheritdoc cref="TryGetResidentScene(ISceneEntry, out Scene)"/>
        public static bool TryGetResidentScene(ThreadlinkIDs.Addressables.Scenes pointer, out Scene scene)
        {
            if (Records.TryGetValue(pointer, out var record) && record.State is SceneState.Resident)
            {
                scene = record.Scene;
                return scene.IsValid() && scene.isLoaded;
            }

            scene = default;
            return false;
        }
        #endregion

        #region Loading and unloading:
        /// <summary>A hold let go: once none remain, the scene unloads, now or after <paramref name="delaySeconds"/>.</summary>
        internal static void Release(SceneRecord record, float delaySeconds)
        {
            if (record.Holds <= 0 || --record.Holds > 0 || !IsCurrent(record))
                return;

            // Still loading: the load ends by unloading it, since nothing holds it any more.
            if (record.State is not SceneState.Resident and not SceneState.UnloadFailed)
                return;

            if (delaySeconds > 0f)
                DelayedUnloadAsync(record, delaySeconds).Forget();
            else
                UnloadAsync(record).Forget();
        }

        /// <summary>The one load path: load additively, boot the scene's objects, run the entry's hook, then publish.</summary>
        private static async UniTaskVoid LoadAsync(SceneRecord record)
        {
            bool loaded = false;

            try
            {
                if (Threadlink.TryGetSingleton(out var core) && core.TryGetSceneReference(record.Pointer, out var reference))
                {
                    var instance = await reference.LoadAsync(new LoadSceneParameters(LoadSceneMode.Additive, record.Entry.PhysicsMode));

                    record.Scene = instance.Scene;

                    if (record.Scene.IsValid())
                    {
                        await Initium.BootAndInitSceneObjectsAsync(record.Scene);
                        await record.Entry.OnFinishedLoadingAsync();
                        loaded = true;
                    }
                }
            }
            catch (Exception exception)
            {
                Scribe.Send<Threadlink>("Loading ", record.Pointer.ToString(), " failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
            }

            // Threadlink shut down while the scene loaded: Unity unloads what is left.
            if (!IsCurrent(record))
            {
                record.Loaded.TrySetResult(false);
                return;
            }

            if (!loaded)
            {
                Scribe.Send<Threadlink>("Could not load ", record.Pointer.ToString(), ".").ToUnityConsole(DebugType.Error);
                await AbandonAsync(record);
                return;
            }

            record.State = SceneState.Resident;
            Publish(ThreadlinkIDs.Iris.Events.OnSceneResident, record.Entry);

            // Decided before the holders resume: they run inside TrySetResult, and one releasing its hold there must go
            // through its own release (and its delay), not be mistaken for a scene nobody wanted.
            bool unwanted = record.Holds == 0;

            record.Loaded.TrySetResult(true);

            if (unwanted && record.Holds == 0 && IsCurrent(record))
                UnloadAsync(record).Forget();
        }

        private static async UniTaskVoid DelayedUnloadAsync(SceneRecord record, float delaySeconds)
        {
            var cancellation = new CancellationTokenSource();

            record.PendingUnload = cancellation;

            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), DelayType.Realtime, PlayerLoopTiming.Update, cancellation.Token)
            .SuppressCancellationThrow();

            if (ReferenceEquals(record.PendingUnload, cancellation))
                record.PendingUnload = null;

            cancellation.Dispose();

            if (!cancelled && record.Holds == 0 && IsCurrent(record))
                await UnloadAsync(record);
        }

        /// <summary>
        /// The one unload path: the entry's hook, <see cref="ThreadlinkIDs.Iris.Events.OnSceneUnloading"/>, every
        /// <see cref="LinkableBehaviour"/> in the scene discarded (so each unregisters what it registered), then the unload.
        /// </summary>
        private static async UniTask UnloadAsync(SceneRecord record)
        {
            if (record.State is not SceneState.Resident and not SceneState.UnloadFailed)
                return;

            record.State = SceneState.Unloading;
            record.Unloaded = new UniTaskCompletionSource();
            CancelPendingUnload(record);

            try
            {
                try
                {
                    await record.Entry.OnBeforeUnloadedAsync();
                }
                catch (Exception exception)
                {
                    Scribe.Send<Threadlink>("OnBeforeUnloadedAsync of ", record.Pointer.ToString(), " failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
                }

                Publish(ThreadlinkIDs.Iris.Events.OnSceneUnloading, record.Entry);

                if (!record.Discarded && record.Scene.IsValid() && record.Scene.isLoaded)
                {
                    record.Discarded = true;
                    try { Initium.DiscardSceneObjects(record.Scene); }
                    catch (Exception exception)
                    {
                        Scribe.Send<Threadlink>("Scene discard failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
                    }
                }

                if (Threadlink.TryGetSingleton(out var core))
                    await core.UnloadSceneAsync(record.Pointer);
            }
            catch (Exception exception)
            {
                Scribe.Send<Threadlink>("Unloading ", record.Pointer.ToString(), " failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
            }
            finally
            {
                // A failed unload remains owned and may be retried by a later hold/release or shutdown.
                if (!record.Scene.IsValid() || !record.Scene.isLoaded)
                {
                    if (IsCurrent(record)) Records.Remove(record.Pointer);
                }
                else record.State = SceneState.UnloadFailed;

                record.Unloaded.TrySetResult();
            }
        }

        /// <summary>
        /// A load that failed: its holders get nothing, and a scene that loaded but failed to boot is discarded and unloaded
        /// again, quietly, since it never became resident. New holders wait for that, then try afresh.
        /// </summary>
        private static async UniTask AbandonAsync(SceneRecord record)
        {
            record.State = SceneState.Unloading;
            record.Unloaded = new UniTaskCompletionSource();
            record.Loaded.TrySetResult(false);

            try
            {
                if (record.Scene.IsValid() && record.Scene.isLoaded)
                {
                    record.Discarded = true;
                    try { Initium.DiscardSceneObjects(record.Scene); }
                    catch (Exception exception)
                    {
                        Scribe.Send<Threadlink>("Scene discard failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
                    }

                    if (Threadlink.TryGetSingleton(out var core))
                        await core.UnloadSceneAsync(record.Pointer);
                }
            }
            catch (Exception exception)
            {
                Scribe.Send<Threadlink>("Unloading ", record.Pointer.ToString(), ", which failed to load, failed too: ", exception.ToString()).ToUnityConsole(DebugType.Error);
            }
            finally
            {
                // A failed unload remains owned and may be retried by a later hold/release or shutdown.
                if (!record.Scene.IsValid() || !record.Scene.isLoaded)
                {
                    if (IsCurrent(record)) Records.Remove(record.Pointer);
                }
                else record.State = SceneState.UnloadFailed;

                record.Unloaded.TrySetResult();
            }
        }

        private static void CancelPendingUnload(SceneRecord record)
        {
            var pending = record.PendingUnload;

            record.PendingUnload = null;
            pending?.Cancel();
        }

        private static bool IsCurrent(SceneRecord record) => Records.TryGetValue(record.Pointer, out var current) && ReferenceEquals(current, record);

        /// <summary>Publish a scene event, so a failing listener can't stop a load or an unload halfway.</summary>
        private static void Publish(ThreadlinkIDs.Iris.Events sceneEvent, ISceneEntry entry)
        {
            try
            {
                Iris.Publish(sceneEvent, entry);
            }
            catch (Exception exception)
            {
                Scribe.Send<Threadlink>(sceneEvent.ToString(), " for ", entry.ScenePointer.ToString(), " failed: ", exception.ToString()).ToUnityConsole(DebugType.Error);
            }
        }
        #endregion

        #region Shutdown:
        /// <summary>
        /// Threadlink's shutdown, before the subsystems are discarded, since scene objects depend on them: nothing is
        /// presented, and every scene still held is let go. Each has its objects discarded now and its unload started; Unity
        /// finishes what the application's end cuts short.
        /// </summary>
        internal static void ReleaseAll()
        {
            ClearPresentation(release: true);

            var records = new List<SceneRecord>(Records.Values);

            foreach (var record in records)
            {
                record.Holds = 0;

                if (record.State is SceneState.Resident or SceneState.UnloadFailed)
                    UnloadAsync(record).Forget();
            }

            Records.Clear();
        }

        /// <summary>Play Mode's end: nothing may outlive it, whatever the shutdown reached (domain reload is off).</summary>
        [OnExitingPlayMode]
        private static void ForgetScenes()
        {
            ClearPresentation(release: false);

            foreach (var record in Records.Values)
                CancelPendingUnload(record);

            Records.Clear();
        }
        #endregion
    }
}
