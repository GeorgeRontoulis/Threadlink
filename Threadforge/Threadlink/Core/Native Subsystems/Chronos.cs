namespace Threadlink.Core.NativeSubsystems.Chronos
{
    using Iris;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Mathematics;

    /// <summary>
    /// Threadlink's Time Management Subsystem.
    /// </summary>
    public static class Chronos
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
                    Iris.Publish(Iris.Events.OnGamePaused);
                }
                else if (value.IsSimilarTo(1f))
                {
                    Time.timeScale = value;
                    Iris.Publish(Iris.Events.OnGameResumed);
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
        public static void Start()
        {
            Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Unsubscribe<Action>(Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
        }

        /// <summary>
        /// Start the subsystem's ticking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Stop()
        {
            Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Unsubscribe<Action>(Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearTotalPlaytime() => TotalPlaytime = 0;
        #endregion

        #region Private API:
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Boot()
        {
            Physics.simulationMode = SimulationMode.Script;
            CountTotalPlaytime = true;
            TotalPlaytime = 0;

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
                Iris.Publish(Iris.Events.OnPlaytimeCountTick, TotalPlaytime);
            }
        }

        private static void UpdatePhysicsTime()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            Physics.Simulate(fixedDeltaTime);
            FixedDeltaTime = fixedDeltaTime;
        }
        #endregion
    }
}
