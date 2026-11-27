namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>Announced on Iris when the game pauses (the first pause claim) or resumes (the last claim released).</summary>
    public readonly struct PauseChanged
    {
        /// <summary>Whether the game is now paused.</summary>
        public readonly bool Paused;

        /// <summary>Creates the event. Chronos publishes it.</summary>
        public PauseChanged(bool paused) => Paused = paused;
    }

    /// <summary>
    /// Chronos: game time for the session. Pausing is a claim: the game is paused while any claim is held, so a pause menu,
    /// a disconnected gamepad and a cutscene can each pause without undoing each other. Chronos also counts playtime.
    /// </summary>
    /// <remarks>
    /// While paused, <see cref="Time.timeScale"/> is 0; otherwise it is <see cref="TimeScale"/>. Unscaled time, UI and
    /// Aura fades keep running. Set the time scale through Chronos rather than <see cref="Time.timeScale"/>.
    /// </remarks>
    public sealed class Chronos : ThreadlinkSubsystem<Chronos>
    {
        private sealed class Claim : IDisposable
        {
            private Chronos owner;

            internal Claim(Chronos owner) => this.owner = owner;

            public void Dispose()
            {
                var chronos = owner;
                owner = null;
                chronos?.Release();
            }
        }

        private SubscriptionToken frames;
        private int claims;
        private float scale = 1;
        private float originalScale = 1;
        private double playtime;
        private bool counting = true;

        /// <summary>Whether any pause claim is held.</summary>
        public static bool IsPaused => Instance.claims > 0;

        /// <summary>The number of pause claims held.</summary>
        public static int PauseClaims => Instance.claims;

        /// <summary>The game's time scale while it is not paused, such as 0.5 for slow motion.</summary>
        public static float TimeScale
        {
            get => Instance.scale;
            set
            {
                if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), "The time scale must be zero or more.");
                var chronos = Instance;
                chronos.scale = value;
                chronos.Apply();
            }
        }

        // From ticks: TimeSpan.FromSeconds rounds to whole milliseconds on Unity's class libraries.
        /// <summary>Real time played this session while not paused, plus what <see cref="SetPlaytime"/> restored.</summary>
        public static TimeSpan Playtime => TimeSpan.FromTicks((long)(Instance.playtime * TimeSpan.TicksPerSecond));

        /// <summary>Whether playtime is counted; turn it off in menus that should not count, such as the title screen.</summary>
        public static bool CountsPlaytime
        {
            get => Instance.counting;
            set => Instance.counting = value;
        }

        /// <summary>Sets the playtime, for example from a loaded save.</summary>
        public static void SetPlaytime(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Playtime cannot be negative.");
            Instance.playtime = value.TotalSeconds;
        }

        /// <summary>Pauses the game until the returned claim is disposed. Claims are independent; disposing one twice does nothing.</summary>
        public static IDisposable Pause()
        {
            var chronos = Instance;
            if (chronos.claims++ == 0)
            {
                chronos.Apply();
                Iris.TryPublish(new PauseChanged(true));
            }
            return new Claim(chronos);
        }

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            originalScale = Time.timeScale;
            scale = originalScale;
            frames = Iris.Subscribe<UpdateFrame>(_ => Count());
        }

        /// <inheritdoc />
        protected override void OnSubsystemShutdown()
        {
            frames.Dispose();
            claims = 0;
            Time.timeScale = originalScale;
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(claims > 0 ? "Paused" : "Running at " + scale.ToString("0.##") + "×");
            if (!report.Detailed)
                return;
            report.Section("Time");
            report.Field("Pause claims", claims, claims > 0 ? MonitorTone.Caution : MonitorTone.Muted);
            report.Field("Time scale", scale);
            report.Field("Playtime", Playtime.ToString(@"hh\:mm\:ss"), counting ? MonitorTone.Normal : MonitorTone.Muted);
        }

        private void Count()
        {
            if (counting && claims == 0)
                playtime += Time.unscaledDeltaTime;
        }

        private void Release()
        {
            // A claim outliving the session belongs to a session that already restored time.
            if (State == SubsystemState.ShutDown || claims == 0)
                return;
            if (--claims == 0)
            {
                Apply();
                Iris.TryPublish(new PauseChanged(false));
            }
        }

        private void Apply() => Time.timeScale = claims > 0 ? 0 : scale;
    }
}
