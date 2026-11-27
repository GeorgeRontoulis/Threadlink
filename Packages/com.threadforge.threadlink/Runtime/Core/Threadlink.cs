namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// The Threadlink core. It owns every <see cref="Subsystem"/> of the session: subsystems register at
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>, the core deploys and initializes them before the first
    /// scene loads, and shuts them down in reverse order when the session ends. Code in Awake and Start can use any
    /// subsystem directly.
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

        private static readonly List<Registration> pending = new List<Registration>();
        private static Threadlink current;

        private readonly Threadlink previous;
        private readonly bool isolated;
        private readonly int thread = Environment.CurrentManagedThreadId;
        private readonly List<Registration> registered = new List<Registration>();
        private readonly Dictionary<Type, ThreadlinkSubsystem> subsystems = new Dictionary<Type, ThreadlinkSubsystem>();
        private readonly List<ThreadlinkSubsystem> order = new List<ThreadlinkSubsystem>();
        private readonly List<ThreadlinkSubsystem> initializing = new List<ThreadlinkSubsystem>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool deployed;
        private bool disposed;

        private Threadlink(Threadlink previous, bool isolated)
        {
            this.previous = previous;
            this.isolated = isolated;
        }

        /// <summary>The core of the current session, or null when Threadlink is not deployed.</summary>
        public static Threadlink Current => current;

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

        /// <summary>Whether the current session has finished deploying.</summary>
        public static bool IsDeployed => current != null && current.deployed && !current.disposed;

        /// <summary>Cancelled when the current session ends. Already cancelled when nothing is deployed.</summary>
        public static CancellationToken Lifetime => current != null ? current.LifetimeToken : new CancellationToken(true);

        /// <summary>Unique identity of this session.</summary>
        public Guid SessionId { get; } = Guid.NewGuid();

        /// <summary>Every subsystem of this session in initialization order, followed by any that failed.</summary>
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

        /// <summary>
        /// Registers a subsystem for the next session. Generated code calls this at
        /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> for every subsystem class; call it yourself only for
        /// a subsystem marked <see cref="ManualRegistrationAttribute"/>. After deployment, the subsystem joins the current
        /// session immediately.
        /// </summary>
        public static void Register<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
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

        /// <summary>Adds a subsystem to this session and initializes it, with any subsystem it requires.</summary>
        /// <returns>The subsystem, which may be <see cref="SubsystemState.Failed"/>.</returns>
        public T Add<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            CheckOpen();
            var registration = Find(typeof(T));
            if (registration == null)
                registered.Add(registration = new Registration(typeof(T), static () => new T()));
            return (T)Initialize(registration, null);
        }

        /// <summary>Returns a subsystem of this session, initializing it first when needed.</summary>
        public T Get<T>()
            where T : ThreadlinkSubsystem<T>, new()
        {
            CheckOpen();
            if (subsystems.TryGetValue(typeof(T), out var subsystem) && subsystem.State == SubsystemState.Ready)
                return (T)subsystem;
            return Add<T>();
        }

        /// <summary>
        /// Deploys a separate core that becomes current until it is disposed, for tests and tools. It starts empty: add the
        /// subsystems under test with <see cref="Add{T}"/>. Disposing it shuts it down and restores the previous core.
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
            Array.Sort(registrations, (a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            core.registered.AddRange(registrations);
            foreach (var registration in registrations)
            {
                try
                {
                    core.Initialize(registration, null);
                }
                catch (Exception)
                {
                    // Already reported; the other subsystems still start.
                }
            }
            core.deployed = true;
#if !UNITY_EDITOR
            Application.quitting -= ShutdownCurrent;
            Application.quitting += ShutdownCurrent;
#endif
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

        /// <summary>Shuts this session down: cancels <see cref="Lifetime"/>, then shuts subsystems down in reverse
        /// initialization order. Restores the previous core of an isolated session.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            CheckThread();
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
                    subsystem.RunShutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(new InvalidOperationException("Threadlink: " + subsystem.Name + " failed to shut down.", error));
                }
                finally
                {
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

        internal T Require<T>(ThreadlinkSubsystem requester)
            where T : ThreadlinkSubsystem<T>, new()
        {
            CheckOpen();
            if (!requester.dependencies.Contains(typeof(T)))
                requester.dependencies.Add(typeof(T));
            var registration = Find(typeof(T));
            if (registration == null)
                registered.Add(registration = new Registration(typeof(T), static () => new T()));
            var subsystem = Initialize(registration, requester);
            if (subsystem.State != SubsystemState.Ready && subsystem.State != SubsystemState.Initializing)
                throw new SubsystemUnavailableException(typeof(T), requester.Name + " requires " + subsystem.Name + ", which failed to initialize: " + subsystem.Error?.Message, subsystem.Error);
            return (T)subsystem;
        }

        private ThreadlinkSubsystem Initialize(Registration registration, ThreadlinkSubsystem requester)
        {
            if (subsystems.TryGetValue(registration.Type, out var existing))
            {
                if (existing.State == SubsystemState.Initializing && requester != null)
                    throw new InvalidOperationException("Threadlink subsystem dependency cycle: " + Chain(existing) + ".");
                return existing;
            }
            ThreadlinkSubsystem subsystem;
            try
            {
                subsystem = registration.Create() ?? throw new InvalidOperationException("The factory returned null.");
            }
            catch (Exception error)
            {
                Debug.LogException(new InvalidOperationException("Threadlink: " + registration.Type.Name + " could not be created.", error));
                throw;
            }
            subsystem.core = this;
            subsystem.State = SubsystemState.Initializing;
            subsystems.Add(registration.Type, subsystem);
            subsystem.Link();
            initializing.Add(subsystem);
            try
            {
                subsystem.RunInitialize();
                subsystem.State = SubsystemState.Ready;
                order.Add(subsystem);
            }
            catch (Exception error)
            {
                subsystem.State = SubsystemState.Failed;
                subsystem.Error = error;
                if (error is SubsystemUnavailableException || error.Message.StartsWith("Threadlink subsystem dependency cycle", StringComparison.Ordinal))
                    Debug.LogError("Threadlink: " + subsystem.Name + " failed to initialize. " + error.Message);
                else
                    Debug.LogException(new InvalidOperationException("Threadlink: " + subsystem.Name + " failed to initialize.", error));
            }
            finally
            {
                initializing.Remove(subsystem);
            }
            return subsystem;
        }

        private string Chain(ThreadlinkSubsystem repeated)
        {
            var text = new StringBuilder();
            int start = initializing.IndexOf(repeated);
            for (int i = Math.Max(0, start); i < initializing.Count; i++)
                text.Append(initializing[i].Name).Append(" → ");
            return text.Append(repeated.Name).ToString();
        }

        private Registration Find(Type type)
        {
            foreach (var registration in registered)
                if (registration.Type == type)
                    return registration;
            return null;
        }

        private void Relink()
        {
            foreach (var subsystem in subsystems.Values)
                if (subsystem.State == SubsystemState.Ready || subsystem.State == SubsystemState.Failed)
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
