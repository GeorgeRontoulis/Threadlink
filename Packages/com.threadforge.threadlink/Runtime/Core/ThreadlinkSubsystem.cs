namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>Where a subsystem is in its session.</summary>
    public enum SubsystemState : byte
    {
        /// <summary>Known to the runtime, not yet created.</summary>
        Registered,

        /// <summary><see cref="Subsystem.OnInitialize"/> is running.</summary>
        Initializing,

        /// <summary>Initialized and linked to the core.</summary>
        Ready,

        /// <summary>Initialization threw; see <see cref="Subsystem.Error"/>.</summary>
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
    /// <see cref="Subsystem{TSelf}"/>; the framework registers, creates, initializes and shuts it down.
    /// </summary>
    public abstract class ThreadlinkSubsystem : IMonitorable
    {
        internal Threadlink core;
        internal readonly List<Type> dependencies = new List<Type>();

        /// <summary>The display name: the class name by default.</summary>
        public virtual string Name => GetType().Name;

        /// <summary>Where this subsystem is in its session.</summary>
        public SubsystemState State { get; internal set; }

        /// <summary>Why initialization failed, when <see cref="State"/> is <see cref="SubsystemState.Failed"/>.</summary>
        public Exception Error { get; internal set; }

        /// <summary>The subsystems this one required during initialization.</summary>
        public IReadOnlyList<Type> Dependencies => dependencies;

        /// <summary>The core this subsystem belongs to.</summary>
        public Threadlink Core => core;

        /// <summary>Cancelled when the session shuts down. Pass it to work that must not outlive the session.</summary>
        protected CancellationToken Lifetime => core != null ? core.LifetimeToken : new CancellationToken(true);

        /// <summary>
        /// Returns another subsystem, initializing it first if it has not been. Call it from <see cref="OnInitialize"/>;
        /// the runtime records the dependency, orders startup and shutdown by it, and rejects cycles.
        /// </summary>
        protected T Require<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            if (core == null)
                throw new InvalidOperationException(Name + " is not linked to a Threadlink core.");
            return core.Require<T>(this);
        }

        /// <summary>Called once, before the first scene's Awake (or when added later). Throwing fails only this subsystem
        /// and the subsystems that require it.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called once when the session ends, in reverse initialization order. Must not throw.</summary>
        protected virtual void OnShutdown() { }

        internal void RunInitialize() => OnInitialize();

        internal void RunShutdown() => OnShutdown();

        /// <summary>Describes this subsystem to the Runtime monitor. Defaults to its state.</summary>
        public virtual void Report(MonitorReport report) => report.Summary(State == SubsystemState.Ready ? "Ready" : State.ToString(), State == SubsystemState.Failed);

        internal abstract void Link();

        internal abstract void Unlink();
    }

    /// <summary>
    /// Base class for subsystems. Its static <see cref="Instance"/> and <see cref="TryGetSingleton"/> give access from
    /// anywhere once the core has linked it; derived classes usually expose static methods built on <see cref="Instance"/>.
    /// </summary>
    /// <typeparam name="TSelf">The deriving class.</typeparam>
    public abstract class ThreadlinkSubsystem<TSelf> : ThreadlinkSubsystem
        where TSelf : ThreadlinkSubsystem<TSelf>
    {
        private static TSelf linked;

        /// <summary>The linked instance. Throws <see cref="SubsystemUnavailableException"/> when it is not linked to the
        /// current core, which is the case outside Play mode unless a runtime was deployed explicitly.</summary>
        public static TSelf Instance
        {
            get
            {
                var subsystem = linked;
                if (subsystem != null && ReferenceEquals(subsystem.core, Threadlink.Current) && (subsystem.State == SubsystemState.Ready || subsystem.State == SubsystemState.Initializing))
                    return subsystem;
                throw Unavailable(subsystem);
            }
        }

        /// <summary>Whether the subsystem exists, is initialized and is linked to the current Threadlink core.</summary>
        public static bool TryGetSingleton(out TSelf subsystem)
        {
            subsystem = linked;
            if (subsystem != null && subsystem.State == SubsystemState.Ready && ReferenceEquals(subsystem.core, Threadlink.Current))
                return true;
            subsystem = null;
            return false;
        }

        internal override void Link() => linked = (TSelf)this;

        internal override void Unlink()
        {
            if (ReferenceEquals(linked, this))
                linked = null;
        }

        private static SubsystemUnavailableException Unavailable(TSelf subsystem)
        {
            string name = typeof(TSelf).Name;
            if (Threadlink.Current == null)
                return new SubsystemUnavailableException(
                    typeof(TSelf),
                    name + " is not available: Threadlink is not deployed. It deploys before the first scene loads in Play mode and in players; tests and tools use Threadlink.DeployIsolated()."
                );
            if (subsystem != null && subsystem.State == SubsystemState.Failed && ReferenceEquals(subsystem.core, Threadlink.Current))
                return new SubsystemUnavailableException(typeof(TSelf), name + " failed to initialize: " + subsystem.Error?.Message, subsystem.Error);
            return new SubsystemUnavailableException(typeof(TSelf), name + " is not part of the current Threadlink session.");
        }
    }
}
