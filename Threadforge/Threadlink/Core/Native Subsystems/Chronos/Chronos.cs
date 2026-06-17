namespace Threadlink.Core.NativeSubsystems.Chronos
{
    using Cysharp.Threading.Tasks;
    using Iris;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Mathematics;
    using NativeResources = Shared.ThreadlinkIDs.Addressables.NativeResources;

    /// <summary>
    /// Threadlink's Time Management Subsystem.
    /// </summary>
    public sealed class Chronos : ThreadlinkSubsystem<Chronos>, IAddressablesPreloader, IDependencyConsumer<ChronosConfig>
    {
        #region Public API:
        public enum PlaytimeCountMode : byte { Scaled, Unscaled }

        /// <summary>
        /// Gets or sets the current Timescale.
        /// Setting the timescale using this property only changes Unity's internal Time.TimeScale value.
        /// Use <paramref name="TimeScale"/> to apply the change along with certain events and callbacks instead. Valid Timescale values are 0 and 1.
        /// </summary>
        public static float RawTimeScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.timeScale;
            set
            {
                if (value.IsSimilarTo(0f) || value.IsSimilarTo(1f))
                    Time.timeScale = value;
            }
        }

        /// <summary>
        /// Gets or sets the current Timescale.
        /// Setting the timescale using this property invokes events and callbacks that may be undesired in some situations.
        /// Use <paramref name="RawTimeScale"/> to only change Unity's internal Time.TimeScale value instead. Valid Timescale values are 0 and 1.
        /// </summary>
        public static float TimeScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.timeScale;
            set
            {
                if (value.IsSimilarTo(Time.timeScale)) return;

                if (value.IsSimilarTo(0f))
                {
                    Time.timeScale = value;
                    Iris.Publish(ThreadlinkIDs.Iris.Events.OnGamePaused);
                }
                else if (value.IsSimilarTo(1f))
                {
                    Time.timeScale = value;
                    Iris.Publish(ThreadlinkIDs.Iris.Events.OnGameResumed);
                }
            }
        }

        public static double CurrentFramerate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1d / (double)DeltaTime;
        }

        public static float CurrentTimeSinceDeployment { get; private set; } = 0f;
        public static float TotalPlaytime { get; private set; } = 0f;
        public static float DeltaTime { get; private set; } = 0f;
        public static float SmoothDeltaTime { get; private set; } = 0f;
        public static float FixedDeltaTime { get; private set; } = 0f;
        public static float UnscaledDeltaTime { get; private set; } = 0f;
        public static bool CountTotalPlaytime { get; set; } = true;

        public static PlaytimeCountMode PlaytimeCountingMode { get; set; } = PlaytimeCountMode.Scaled;

        /// <summary>
        /// Stop the subsystem from ticking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start()
        {
            Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
        }

        /// <summary>
        /// Start the subsystem's ticking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stop()
        {
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearTotalPlaytime() => TotalPlaytime = 0;
        #endregion

        #region Private API:
        private ChronosConfig Config { get; set; } = null;

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            if (Threadlink.TryGetSingleton(out var core))
            {
                const NativeResources ID = NativeResources.ChronosConfig;
                return TryConsumeDependency(await core.NativeConfig.LoadNativeResourceAsync<ChronosConfig>(ID));
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(ChronosConfig input) => (Config = input) != null;

        public override void Boot()
        {
            if (Config.IrisPhysicsUpdate)
                Physics.simulationMode = SimulationMode.Script;

            CountTotalPlaytime = true;
            TotalPlaytime = 0;

            base.Boot();
            Start();
        }

        private static void UpdateStandardTime()
        {
            CurrentTimeSinceDeployment = Time.time;
            DeltaTime = Time.deltaTime;
            SmoothDeltaTime = Time.smoothDeltaTime;
            UnscaledDeltaTime = Time.unscaledDeltaTime;

            if (CountTotalPlaytime)
            {
                TotalPlaytime += PlaytimeCountingMode is PlaytimeCountMode.Scaled ? DeltaTime : UnscaledDeltaTime;
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnPlaytimeCountTick, TotalPlaytime);
            }
        }

        private void UpdatePhysicsTime()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;

            if (Config.IrisPhysicsUpdate)
                Physics.Simulate(fixedDeltaTime);

            FixedDeltaTime = fixedDeltaTime;
        }
        #endregion
    }
}
