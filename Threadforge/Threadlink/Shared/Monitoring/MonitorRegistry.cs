namespace Threadlink.Shared
{
    using System.Collections.Generic;
    using Unity.Scripting.LifecycleManagement;

    /// <summary>
    /// Monitored objects that are neither subsystems nor static services, such as a scene singleton or a network
    /// session. Register in <c>Boot</c> and unregister in <c>Discard</c>. Cleared when Play Mode exits.
    /// </summary>
    public static class MonitorRegistry
    {
        public readonly struct Entry
        {
            public string Name { get; }
            public IMonitorable Source { get; }

            internal Entry(string name, IMonitorable source)
            {
                Name = name;
                Source = source;
            }
        }

        [NoAutoStaticsCleanup]
        private static readonly List<Entry> Registered = new(4);

        public static IReadOnlyList<Entry> Entries => Registered;

        /// <summary>Registers <paramref name="source"/> under <paramref name="name"/>, replacing any earlier registration of it.</summary>
        public static void Register(string name, IMonitorable source)
        {
            if (source == null)
                return;

            Unregister(source);
            Registered.Add(new Entry(string.IsNullOrEmpty(name) ? source.GetType().Name : name, source));
        }

        public static void Unregister(IMonitorable source)
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Registered[i].Source, source))
                    Registered.RemoveAt(i);
            }
        }

        [OnExitingPlayMode]
        private static void Clear() => Registered.Clear();
    }
}
