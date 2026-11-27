namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;

    /// <summary>Where a subsystem is in its session.</summary>
    public enum SubsystemState : byte
    {
        /// <summary>Created, not yet booted.</summary>
        Registered,

        /// <summary><see cref="ThreadlinkSubsystem.OnSubsystemBoot"/> is running.</summary>
        Booting,

        /// <summary>Booted: the subsystem has set itself up and waits for the initialization phase.</summary>
        Booted,

        /// <summary><see cref="ThreadlinkSubsystem.OnSubsystemInit"/> is running.</summary>
        Initializing,

        /// <summary>Booted and initialized.</summary>
        Ready,

        /// <summary>Boot or initialization threw; see <see cref="ThreadlinkSubsystem.Error"/>.</summary>
        Failed,

        /// <summary>Shut down with its session.</summary>
        ShutDown,
    }

    /// <summary>Thrown when a subsystem is used while it is not linked to the Threadlink core.</summary>
    public sealed class SubsystemUnavailableException : InvalidOperationException
    {
        /// <summary>The subsystem that was requested.</summary>
        public Type SubsystemType { get; }

        internal SubsystemUnavailableException(Type subsystem, string message, Exception inner = null)
            : base(message, inner) => SubsystemType = subsystem;
    }

    /// <summary>Opts a subsystem out of automatic registration. Add it explicitly with <see cref="Threadlink.Add{T}"/>.</summary>
    /// <remarks>Use it for test and tool subsystems that must not join the game's session.</remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ManualRegistrationAttribute : Attribute { }

    /// <summary>
    /// A part of the game that lives for the whole session, owned by the <see cref="Threadlink"/> core. Derive from
    /// <see cref="ThreadlinkSubsystem{TSelf}"/>; the framework registers, creates, starts and shuts it down.
    /// </summary>
    /// <remarks>
    /// A session starts in two phases. Every subsystem boots first (<see cref="OnSubsystemBoot"/>): it sets up only
    /// itself and may subscribe to <see cref="Iris"/>, which boots before all others. Then every subsystem initializes
    /// (<see cref="OnSubsystemInit"/>): all subsystems have booted, so any of them can be used, in any order. Shutdown
    /// mirrors it: <see cref="OnSubsystemDeinit"/> everywhere, then <see cref="OnSubsystemShutdown"/>.
    /// </remarks>
    public abstract class ThreadlinkSubsystem : IMonitorable
    {
        private static readonly Dictionary<Type, bool> legacyTypes = new Dictionary<Type, bool>();

        internal Threadlink core;
        internal readonly List<Type> dependencies = new List<Type>();
        internal bool legacy;

        /// <summary>The display name: the class name by default.</summary>
        public virtual string Name => GetType().Name;

        /// <summary>Where this subsystem is in its session.</summary>
        public SubsystemState State { get; internal set; }

        /// <summary>Why boot or initialization failed, when <see cref="State"/> is <see cref="SubsystemState.Failed"/>.</summary>
        public Exception Error { get; internal set; }

        /// <summary>The subsystems this one used while it initialized, for the Runtime monitor.</summary>
        public IReadOnlyList<Type> Dependencies => dependencies;

        /// <summary>The core this subsystem belongs to.</summary>
        public Threadlink Core => core;

        /// <summary>Cancelled when the session shuts down. Pass it to work that must not outlive the session.</summary>
        protected CancellationToken Lifetime => core != null ? core.LifetimeToken : new CancellationToken(true);

        /// <summary>
        /// Phase one of startup: set up this subsystem's own state, and subscribe to <see cref="Iris"/> events. Runs before
        /// the first scene's Awake. Other subsystems are not available yet; using one throws.
        /// </summary>
        /// <remarks>Throwing fails this subsystem; the others still start.</remarks>
        protected virtual void OnSubsystemBoot() { }

        /// <summary>
        /// Phase two of startup: connect to other subsystems and announce readiness on <see cref="Iris"/>. Every subsystem
        /// has booted, so any can be used through its <c>Instance</c>, in any order.
        /// </summary>
        /// <remarks>
        /// Read state another subsystem only reaches in its own initialization (the signed-in account, a platform) through
        /// its events or when it is used, not here. Throwing fails this subsystem; the others still start.
        /// </remarks>
        protected virtual void OnSubsystemInit() { }

        /// <summary>Phase one of shutdown: disconnect from other subsystems. Runs in reverse initialization order, while every
        /// subsystem is still available. Must not throw.</summary>
        protected virtual void OnSubsystemDeinit() { }

        /// <summary>Phase two of shutdown: release this subsystem's own state. Runs in reverse boot order. Must not throw.</summary>
        protected virtual void OnSubsystemShutdown() { }

        /// <summary>Called during the initialization phase. Its subsystem is initialized as soon as another uses it.</summary>
        [Obsolete("Split it: set up the subsystem itself in OnSubsystemBoot, connect to other subsystems in OnSubsystemInit.")]
        protected virtual void OnInitialize() { }

        /// <summary>Called during the first shutdown phase, while every subsystem is still available.</summary>
        [Obsolete("Split it: disconnect from other subsystems in OnSubsystemDeinit, release the subsystem's own state in OnSubsystemShutdown.")]
        protected virtual void OnShutdown() { }

        /// <summary>Returns another subsystem, initializing it first when it has not been.</summary>
        [Obsolete("Use T.Instance from OnSubsystemInit: every subsystem has booted by then, so no startup order needs declaring.")]
        protected T Require<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            if (core == null)
                throw new InvalidOperationException(Name + " is not linked to a Threadlink core.");
            return core.Require<T>(this);
        }

        /// <summary>Describes this subsystem to the Runtime monitor. Defaults to its state.</summary>
        public virtual void Report(MonitorReport report) => report.Summary(State == SubsystemState.Ready ? "Ready" : State.ToString(), State == SubsystemState.Failed);

        internal void RunBoot() => OnSubsystemBoot();

        internal void RunInit()
        {
            OnSubsystemInit();
#pragma warning disable CS0618 // The obsolete hook still runs for subsystems written against 1.0.
            OnInitialize();
#pragma warning restore CS0618
        }

        internal void RunDeinit()
        {
            OnSubsystemDeinit();
#pragma warning disable CS0618
            OnShutdown();
#pragma warning restore CS0618
        }

        internal void RunShutdown() => OnSubsystemShutdown();

        // A subsystem that still overrides OnInitialize does its own setup there, so it is initialized as soon as another
        // subsystem uses it, as 1.0 guaranteed.
        internal static bool IsLegacy(Type type)
        {
            lock (legacyTypes)
            {
                if (legacyTypes.TryGetValue(type, out bool legacy))
                    return legacy;
                var method = type.GetMethod("OnInitialize", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                legacy = method != null && method.DeclaringType != typeof(ThreadlinkSubsystem);
                legacyTypes.Add(type, legacy);
                return legacy;
            }
        }

        internal abstract void Link();

        internal abstract void Unlink();
    }

    /// <summary>
    /// Base class for subsystems. Its static <see cref="Instance"/> and <see cref="TryGetSingleton"/> give access from
    /// anywhere once the core has booted it; derived classes usually expose static methods built on <see cref="Instance"/>.
    /// </summary>
    /// <typeparam name="TSelf">The deriving class.</typeparam>
    public abstract class ThreadlinkSubsystem<TSelf> : ThreadlinkSubsystem
        where TSelf : ThreadlinkSubsystem<TSelf>
    {
        private static TSelf linked;
        private static Func<ThreadlinkSubsystem> factory;

        /// <summary>
        /// The subsystem in the current session. Throws <see cref="SubsystemUnavailableException"/> when it is not part of
        /// the session or failed to start, and <see cref="InvalidOperationException"/> when another subsystem uses it from
        /// <see cref="ThreadlinkSubsystem.OnSubsystemBoot"/>.
        /// </summary>
        /// <remarks>Outside Play mode it is available only in a session deployed explicitly with <see cref="Threadlink.DeployIsolated"/>.</remarks>
        public static TSelf Instance
        {
            get
            {
                var subsystem = linked;
                if (subsystem != null && subsystem.State == SubsystemState.Ready && subsystem.core.IsSettled && ReferenceEquals(subsystem.core, Threadlink.Current))
                    return subsystem;
                return (TSelf)Threadlink.Resolve(typeof(TSelf), factory ?? Create, false);
            }
        }

        /// <summary>Whether the subsystem is part of the current session and available, returning it. Never starts it.</summary>
        public static bool TryGetSingleton(out TSelf subsystem)
        {
            subsystem = linked;
            if (subsystem != null && subsystem.State == SubsystemState.Ready && subsystem.core.IsSettled && ReferenceEquals(subsystem.core, Threadlink.Current))
                return true;
            subsystem = (TSelf)Threadlink.Resolve(typeof(TSelf), null, true);
            return subsystem != null;
        }

        // The linked instance without the session checks of Instance, for framework code that runs at any phase.
        internal static TSelf Linked => linked;

        internal static void UseFactory(Func<ThreadlinkSubsystem> create) => factory ??= create;

        private static ThreadlinkSubsystem Create() => (ThreadlinkSubsystem)Activator.CreateInstance(typeof(TSelf), true);

        internal override void Link() => linked = (TSelf)this;

        internal override void Unlink()
        {
            if (ReferenceEquals(linked, this))
                linked = null;
        }
    }
}
