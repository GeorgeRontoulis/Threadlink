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
    [Serializable]
    public sealed class Chronos : ThreadlinkSubsystem<Chronos>
    {
        public enum PlaytimeCountMode : byte { Scaled, Unscaled }

        /// <summary>
        /// Gets or sets the current Timescale.
        /// Setting the timescale using this property only changes Unity's internal Time.TimeScale value.
        /// Use <paramref name="TimeScale"/> to apply the change along with certain events and callbacks instead. Valid Timescale values are 0 and 1.
        /// </summary>
        public float RawTimeScale
        {
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
        public float TimeScale
        {
            get => Time.timeScale;
            set
            {
                if (value.IsSimilarTo(Time.timeScale)) return;

                void UpdateTimescale() => Time.timeScale = value;

                if (value.IsSimilarTo(0f))
                {
                    UpdateTimescale();
                    Iris.Publish(Iris.Events.OnGamePaused);
                }
                else if (value.IsSimilarTo(1f))
                {
                    UpdateTimescale();
                    Iris.Publish(Iris.Events.OnGameResumed);
                }
            }
        }

        public double CurrentFramerate => 1d / (double)DeltaTime;

        public float CurrentTimeSinceDeployment { get; private set; } = 0f;
        public float TotalPlaytime { get; private set; } = 0f;
        public float DeltaTime { get; private set; } = 0f;
        public float SmoothDeltaTime { get; private set; } = 0f;
        public float FixedDeltaTime { get; private set; } = 0f;
        public float UnscaledDeltaTime { get; private set; } = 0f;
        public bool CountTotalPlaytime { get; private set; } = false;

        public PlaytimeCountMode PlaytimeCountingMode { get; set; } = PlaytimeCountMode.Scaled;

        public override void Discard()
        {
            Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Unsubscribe<Action>(Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
            base.Discard();
        }

        public override void Boot()
        {
            base.Boot();
            Physics.simulationMode = SimulationMode.Script;
            CountTotalPlaytime = true;
            TotalPlaytime = 0;

            Iris.Subscribe<Action>(Iris.Events.OnUpdate, UpdateStandardTime);
            Iris.Subscribe<Action>(Iris.Events.OnFixedUpdate, UpdatePhysicsTime);
        }

        private void UpdateStandardTime()
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

        private void UpdatePhysicsTime()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            Physics.Simulate(fixedDeltaTime);
            FixedDeltaTime = fixedDeltaTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearTotalPlaytime() => TotalPlaytime = 0;
    }
}
