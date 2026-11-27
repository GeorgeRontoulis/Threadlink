namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// The Threadlink core. It owns every <see cref="ThreadlinkSubsystem"/> of the session: subsystems register at
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>, and the core starts them before the first scene loads,
    /// in two phases. <see cref="Iris"/> boots and initializes first; then every other subsystem boots, then every one
    /// initializes. Shutdown mirrors it. Code in Awake and Start can use any subsystem directly.
    /// </summary>
    public sealed partial class Threadlink : IMonitorable, IDisposable
    {
        private sealed class Registration
        {
            internal readonly Type Type;
            internal readonly Func<ThreadlinkSubsystem> Create;

            internal Registration(Type type, Func<ThreadlinkSubsystem> create)
            {
                Type = type;
                Create = create;
            }
        }

        /// <summary>Thrown when a subsystem uses another one from <see cref="ThreadlinkSubsystem.OnSubsystemBoot"/>.</summary>
        private sealed class BootPhaseException : InvalidOperationException
        {
            internal BootPhaseException(string message)
                : base(message) { }
        }

        private static readonly List<Registration> pending = new List<Registration>();
        private static Threadlink current;

        private readonly Threadlink previous;
        private readonly bool isolated;
        private readonly int thread = Environment.CurrentManagedThreadId;
        private readonly Dictionary<Type, ThreadlinkSubsystem> subsystems = new Dictionary<Type, ThreadlinkSubsystem>();
        private readonly List<ThreadlinkSubsystem> booted = new List<ThreadlinkSubsystem>();
        private readonly List<ThreadlinkSubsystem> order = new List<ThreadlinkSubsystem>();
        private readonly List<ThreadlinkSubsystem> initializing = new List<ThreadlinkSubsystem>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private ThreadlinkSubsystem booting;
        private Iris iris;
        private int busy;
        private bool deployed;
        private bool disposed;

        private Threadlink(Threadlink previous, bool isolated)
        {
            this.previous = previous;
            this.isolated = isolated;
        }

        // The current core, also while it deploys. Code outside the framework uses Instance or TryGetSingleton.
        internal static Threadlink Current => current;

        /// <summary>The core of the current session. Throws when Threadlink is not deployed.</summary>
        public static Threadlink Instance =>
            current ?? throw new InvalidOperationException("Threadlink is not deployed. It deploys before the first scene loads in Play mode and in players; tests and tools use Threadlink.DeployIsolated().");

        /// <summary>Whether a core is deployed and linked, returning it.</summary>
        public static bool TryGetSingleton(out Threadlink core)
        {
            core = current;
            if (core != null && core.deployed && !core.disposed)
                return true;
            core = null;
            return false;
        }

        /// <summary>Whether the current session has finished starting.</summary>
        public static bool IsDeployed => current != null && current.deployed && !current.disposed;

        /// <summary>Cancelled when the current session ends. Already cancelled when nothing is deployed.</summary>
        public static CancellationToken Lifetime => current != null ? current.LifetimeToken : new CancellationToken(true);

        /// <summary>Unique identity of this session.</summary>
        public Guid SessionId { get; } = Guid.NewGuid();

        /// <summary>Every subsystem of this session in initialization order, followed by any that did not initialize.</summary>
        public IReadOnlyList<ThreadlinkSubsystem> Subsystems
        {
            get
            {
                var all = new List<ThreadlinkSubsystem>(order);
                foreach (var subsystem in subsystems.Values)
                    if (!all.Contains(subsystem))
                        all.Add(subsystem);
                return all;
            }
        }

        internal CancellationToken LifetimeToken => disposed ? new CancellationToken(true) : lifetime.Token;

        // No subsystem is booting or initializing: a ready subsystem can be returned without bookkeeping.
        internal bool IsSettled => busy == 0;

        /// <summary>
        /// Registers a subsystem for the next session. Generated code calls this at
        /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> for every subsystem class; call it yourself only for
        /// a subsystem marked <see cref="ManualRegistrationAttribute"/>. After deployment, the subsystem joins the current
        /// session immediately.
        /// </summary>
        public static void Register<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            ThreadlinkSubsystem<T>.UseFactory(static () => new T());
            if (current != null && current.deployed && !current.disposed)
            {
                current.Add<T>();
                return;
            }
            foreach (var registration in pending)
                if (registration.Type == typeof(T))
                    return;
            pending.Add(new Registration(typeof(T), static () => new T()));
        }

        /// <summary>Adds a subsystem to this session: it boots and initializes now. Returns the existing one when present.</summary>
        /// <returns>The subsystem, which may be <see cref="SubsystemState.Failed"/>.</returns>
        public T Add<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            CheckOpen();
            ThreadlinkSubsystem<T>.UseFactory(static () => new T());
            if (subsystems.TryGetValue(typeof(T), out var existing))
            {
                Initialize(existing);
                return (T)existing;
            }
            return (T)Start(typeof(T), static () => new T());
        }

        /// <summary>
        /// Deploys a separate core that becomes current until it is disposed, for tests and tools. It starts empty: add the
        /// subsystems under test with <see cref="Add{T}"/>; subsystems they use join on first use. Disposing it shuts it down
        /// and restores the previous core.
        /// </summary>
        public static Threadlink DeployIsolated()
        {
            var core = new Threadlink(current, true);
            current = core;
            core.deployed = true;
            return core;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DeploySession()
        {
            ShutdownCurrent();
            var core = new Threadlink(null, false);
            current = core;
            var registrations = pending.ToArray();
            pending.Clear();
#if !UNITY_EDITOR
            Application.quitting -= ShutdownCurrent;
            Application.quitting += ShutdownCurrent;
#endif
            core.StartAll(registrations);
        }

        // Starts subsystems the way a session deploys them, in a separate core, for tests.
        internal static Threadlink DeployIsolated(params Type[] types)
        {
            var core = new Threadlink(current, true);
            current = core;
            var registrations = new Registration[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                registrations[i] = new Registration(type, () => (ThreadlinkSubsystem)Activator.CreateInstance(type, true));
            }
            core.StartAll(registrations);
            return core;
        }

        private void StartAll(Registration[] registrations)
        {
            // Iris first, then by name, so every session starts in the same order.
            Array.Sort(registrations, (a, b) => a.Type == b.Type ? 0 : a.Type == typeof(Iris) ? -1 : b.Type == typeof(Iris) ? 1 : string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            var created = new List<ThreadlinkSubsystem>(registrations.Length);
            foreach (var registration in registrations)
            {
                try
                {
                    created.Add(Create(registration.Type, registration.Create));
                }
                catch (Exception)
                {
                    // Already reported; the other subsystems still start.
                }
            }
            // Iris carries the startup events, so it is ready before anything else boots.
            if (iris != null)
            {
                Boot(iris);
                Initialize(iris);
            }
            foreach (var subsystem in created)
                Boot(subsystem);
            foreach (var subsystem in created)
                Initialize(subsystem);
            deployed = true;
            Announce(ReadyEvent());
        }

        /// <summary>Shuts down every session, isolated ones included. The Editor calls it when Play mode ends and before
        /// code reloads; players call it on quit.</summary>
        internal static void ShutdownCurrent()
        {
            while (current != null)
                current.Dispose();
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        private static void OnCodeUnloading()
        {
            ShutdownCurrent();
            pending.Clear();
        }

        /// <summary>
        /// Shuts this session down: announces <see cref="SessionEnding"/>, cancels <see cref="Lifetime"/>, deinitializes every
        /// subsystem in reverse initialization order, then shuts each down in reverse boot order. Restores the previous core of
        /// an isolated session.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;
            CheckThread();
            Announce(new SessionEnding(SessionId));
            disposed = true;
            try
            {
                lifetime.Cancel();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var subsystem = order[i];
                try
                {
                    subsystem.RunDeinit();
                }
                catch (Exception error)
                {
                    Debug.LogException(new InvalidOperationException("Threadlink: " + subsystem.Name + " failed to deinitialize.", error));
                }
            }
            for (int i = booted.Count - 1; i >= 0; i--)
            {
                var subsystem = booted[i];
                try
                {
                    subsystem.RunShutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(new InvalidOperationException("Threadlink: " + subsystem.Name + " failed to shut down.", error));
                }
                finally
                {
                    if (subsystem.State != SubsystemState.Failed)
                        subsystem.State = SubsystemState.ShutDown;
                }
            }
            foreach (var subsystem in subsystems.Values)
            {
                subsystem.Unlink();
                if (subsystem.State != SubsystemState.Failed)
                    subsystem.State = SubsystemState.ShutDown;
            }
            lifetime.Dispose();
            if (ReferenceEquals(current, this))
            {
                current = previous;
                previous?.Relink();
            }
        }

        /// <inheritdoc />
        public void Report(MonitorReport report)
        {
            int ready = 0,
                failed = 0;
            foreach (var subsystem in subsystems.Values)
                if (subsystem.State == SubsystemState.Ready)
                    ready++;
                else if (subsystem.State == SubsystemState.Failed)
                    failed++;
            report.Summary(disposed ? "Shut down" : !deployed ? "Deploying" : failed > 0 ? ready + " ready, " + failed + " failed" : ready + " ready", failed > 0);
            if (!report.Detailed)
                return;
            report.Section("Session");
            report.Field("State", disposed ? "Shut down" : deployed ? "Deployed" : "Deploying", disposed ? MonitorTone.Muted : deployed ? MonitorTone.Good : MonitorTone.Caution);
            report.Field("Isolated", isolated);
            report.Field("Session", SessionId.ToString("N").Substring(0, 8));
            report.Section("Initialization order");
            for (int i = 0; i < order.Count; i++)
                report.Field((i + 1) + ".  " + order[i].Name, order[i].State.ToString(), order[i].State == SubsystemState.Ready ? MonitorTone.Muted : MonitorTone.Caution);
            foreach (var subsystem in subsystems.Values)
                if (subsystem.State == SubsystemState.Failed)
                    report.Field(subsystem.Name, subsystem.Error?.Message ?? "Failed", MonitorTone.Bad);
            ReportOperations(report);
        }

        /// <summary>
        /// The subsystem of <paramref name="type"/> in the current session, for <c>Instance</c> and <c>TryGetSingleton</c>.
        /// Enforces the boot phase, records what an initializing subsystem uses, and starts a subsystem that is not part of
        /// the session yet when <paramref name="create"/> is given. Returns null instead of throwing when
        /// <paramref name="trying"/>.
        /// </summary>
        internal static ThreadlinkSubsystem Resolve(Type type, Func<ThreadlinkSubsystem> create, bool trying)
        {
            var core = current;
            if (core == null)
                return trying
                    ? null
                    : throw new SubsystemUnavailableException(
                        type,
                        type.Name + " is not available: Threadlink is not deployed. It deploys before the first scene loads in Play mode and in players; tests and tools use Threadlink.DeployIsolated()."
                    );
            var bootingNow = core.booting;
            if (bootingNow != null && bootingNow.GetType() != type && type != typeof(Iris))
                return trying
                    ? null
                    : throw new BootPhaseException(
                        bootingNow.Name + " used " + type.Name + " in OnSubsystemBoot. Boot sets up a subsystem on its own; use other subsystems from OnSubsystemInit. Only Iris is available while booting."
                    );
            var requester = core.initializing.Count > 0 ? core.initializing[core.initializing.Count - 1] : null;
            if (!core.subsystems.TryGetValue(type, out var subsystem))
            {
                if (trying || create == null || core.disposed)
                    return trying ? null : throw new SubsystemUnavailableException(type, type.Name + " is not part of the current Threadlink session.");
                if (!core.isolated && type.IsDefined(typeof(ManualRegistrationAttribute), false))
                    throw new SubsystemUnavailableException(
                        type,
                        type.Name + " is not part of the current Threadlink session. It is marked [ManualRegistration]: add it with Threadlink.Instance.Add<" + type.Name + ">()."
                    );
                core.CheckThread();
                subsystem = core.Start(type, create);
            }
            switch (subsystem.State)
            {
                case SubsystemState.Booting:
                case SubsystemState.Booted:
                case SubsystemState.Initializing:
                case SubsystemState.Ready:
                    break;
                case SubsystemState.Failed:
                    return trying
                        ? null
                        : throw new SubsystemUnavailableException(
                            type,
                            (requester != null && !ReferenceEquals(requester, subsystem) ? requester.Name + " uses " + subsystem.Name + ", which" : subsystem.Name)
                                + " failed to start: "
                                + subsystem.Error?.Message,
                            subsystem.Error
                        );
                default:
                    return trying
                        ? null
                        : throw new SubsystemUnavailableException(type, subsystem.Name + " is not available: it is " + subsystem.State + ".");
            }
            if (requester != null && !ReferenceEquals(requester, subsystem) && !requester.dependencies.Contains(type))
                requester.dependencies.Add(type);
            return subsystem;
        }

        // Delivers a framework event when this session's Iris is up; listener failures are reported, not thrown.
        internal void Announce<T>(T value)
        {
            var bus = iris != null && (iris.State == SubsystemState.Ready || iris.State == SubsystemState.Initializing || iris.State == SubsystemState.Booted) ? iris.Bus : null;
            bus?.PublishIsolated(value);
        }

        private ThreadlinkReady ReadyEvent()
        {
            int ready = 0,
                failed = 0;
            foreach (var subsystem in subsystems.Values)
                if (subsystem.State == SubsystemState.Ready)
                    ready++;
                else if (subsystem.State == SubsystemState.Failed)
                    failed++;
            return new ThreadlinkReady(SessionId, ready, failed);
        }

        private ThreadlinkSubsystem Start(Type type, Func<ThreadlinkSubsystem> create)
        {
            var subsystem = Create(type, create);
            if (subsystem is Iris)
            {
                // Iris boots alone, even when another subsystem's boot asked for it.
                var outer = booting;
                booting = null;
                try
                {
                    Boot(subsystem);
                }
                finally
                {
                    booting = outer;
                }
            }
            else
            {
                Boot(subsystem);
            }
            Initialize(subsystem);
            return subsystem;
        }

        private ThreadlinkSubsystem Create(Type type, Func<ThreadlinkSubsystem> create)
        {
            ThreadlinkSubsystem subsystem;
            try
            {
                subsystem = create() ?? throw new InvalidOperationException("The factory returned null.");
            }
            catch (Exception error)
            {
                Debug.LogException(new InvalidOperationException("Threadlink: " + type.Name + " could not be created.", error));
                throw;
            }
            subsystem.core = this;
            subsystem.State = SubsystemState.Registered;
            subsystems.Add(type, subsystem);
            if (subsystem is Iris created)
                iris = created;
            subsystem.Link();
            return subsystem;
        }

        private void Boot(ThreadlinkSubsystem subsystem)
        {
            if (subsystem.State != SubsystemState.Registered)
                return;
            var outer = booting;
            booting = subsystem;
            busy++;
            subsystem.State = SubsystemState.Booting;
            try
            {
                subsystem.RunBoot();
                subsystem.State = SubsystemState.Booted;
                booted.Add(subsystem);
            }
            catch (Exception error)
            {
                Fail(subsystem, error, "boot");
            }
            finally
            {
                booting = outer;
                busy--;
            }
        }

        private void Initialize(ThreadlinkSubsystem subsystem)
        {
            if (subsystem.State != SubsystemState.Booted)
                return;
            // An initialization started from another subsystem's boot runs outside that boot.
            var outer = booting;
            booting = null;
            initializing.Add(subsystem);
            busy++;
            subsystem.State = SubsystemState.Initializing;
            try
            {
                subsystem.RunInit();
                subsystem.State = SubsystemState.Ready;
                order.Add(subsystem);
            }
            catch (Exception error)
            {
                Fail(subsystem, error, "initialize");
            }
            finally
            {
                initializing.Remove(subsystem);
                busy--;
                booting = outer;
            }
        }

        private void Fail(ThreadlinkSubsystem subsystem, Exception error, string phase)
        {
            subsystem.State = SubsystemState.Failed;
            subsystem.Error = error;
            if (error is SubsystemUnavailableException || error is BootPhaseException)
                Debug.LogError("Threadlink: " + subsystem.Name + " failed to " + phase + ". " + error.Message);
            else
                Debug.LogException(new InvalidOperationException("Threadlink: " + subsystem.Name + " failed to " + phase + ".", error));
            Announce(new SubsystemFailed(subsystem.GetType(), subsystem.Name, error));
        }

        private void Relink()
        {
            foreach (var subsystem in subsystems.Values)
                if (subsystem.State != SubsystemState.ShutDown)
                    subsystem.Link();
        }

        private void CheckThread()
        {
            if (Environment.CurrentManagedThreadId != thread)
                throw new InvalidOperationException("The Threadlink core is used from the main thread only.");
        }

        private void CheckOpen()
        {
            CheckThread();
            if (disposed)
                throw new ObjectDisposedException(nameof(Threadlink), "This Threadlink session has shut down.");
        }
    }
}
