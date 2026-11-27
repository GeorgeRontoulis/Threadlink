namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>The session's content loading: one provider over every generated catalog.</summary>
    internal sealed partial class ContentLibrary : ThreadlinkSubsystem<ContentLibrary>
    {
        private static readonly Dictionary<Guid, ContentEntry> entries = new Dictionary<Guid, ContentEntry>();

        /// <summary>Replaces the Addressables backend in tests.</summary>
        internal static Func<IContentBackend> BackendOverride;

        private ContentCatalog catalog;

        internal ContentProvider Provider { get; private set; }

        public override string Name => "Content";

        internal static void AddEntries(IEnumerable<ContentEntry> mappings)
        {
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));
            foreach (var entry in mappings)
            {
                if (entry == null)
                    throw new ArgumentException("A catalog entry is null.", nameof(mappings));
                if (entries.TryGetValue(entry.Identity, out var existing) && (existing.AssetType != entry.AssetType || existing.Location != entry.Location))
                    throw new ArgumentException("Two catalogs map " + entry.Identity.ToString("D") + " differently.");
                entries[entry.Identity] = entry;
                // Not TryGetSingleton: a subsystem may map content while it boots, when other subsystems are off limits.
                var library = Linked;
                if (library != null && library.catalog != null && ReferenceEquals(library.Core, Threadlink.Current))
                    library.catalog.Merge(entry);
            }
        }

        // The Addressables key of a reference: its asset GUID, with the sub-object in brackets when it names one.
        private static bool TryKey(UnityEngine.AddressableAssets.AssetReference reference, out string key)
        {
            key = reference != null && reference.RuntimeKeyIsValid() ? reference.RuntimeKey.ToString() : null;
            return key != null;
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        private static void OnCodeUnloading() => entries.Clear();

        protected override void OnSubsystemBoot()
        {
            // The mapping in ContentSettings first; entries mapped from code (Content.Map) replace an ID they share.
            var all = new Dictionary<Guid, ContentEntry>();
            var settings = ContentSettings.Current;
            foreach (var pair in settings.Assets)
                if (TryKey(pair.Value, out string key))
                    all[pair.Key.Identity] = ContentEntry.Create(pair.Key.Identity, typeof(UnityEngine.Object), key);
            foreach (var pair in settings.Prefabs)
                if (TryKey(pair.Value, out string key))
                    all[pair.Key.Identity] = ContentEntry.Create(pair.Key.Identity, typeof(UnityEngine.GameObject), key);
            foreach (var pair in settings.Scenes)
                if (TryKey(pair.Value, out string key))
                    all[pair.Key.Identity] = ContentEntry.Create(pair.Key.Identity, typeof(UnityEngine.ResourceManagement.ResourceProviders.SceneInstance), key);
            foreach (var entry in entries.Values)
                all[entry.Identity] = entry;
            catalog = new ContentCatalog(all.Values);
            Provider = new ContentProvider(catalog, BackendOverride?.Invoke() ?? new AddressablesBackend());
        }

        internal void UseBackend(IContentBackend backend) =>
            Provider = new ContentProvider(catalog, backend ?? BackendOverride?.Invoke() ?? new AddressablesBackend());

        // After every subsystem has deinitialized and released what it held.
        protected override void OnSubsystemShutdown()
        {
            foreach (var status in Provider.GetOutstanding())
                UnityEngine.Debug.LogWarning("Threadlink: content " + status.ContentId.ToString("D") + " (" + status.Kind + ") was still held when the session ended.");
        }

        public override void Report(MonitorReport report)
        {
            int outstanding = Provider.OutstandingRequests;
            report.Summary(outstanding + " held");
            if (!report.Detailed)
                return;
            report.Section("Catalog");
            report.Field("Mapped identities", catalog.Count);
            report.Section("Held requests  (" + outstanding + ")");
            int shown = 0;
            foreach (var status in Provider.GetOutstanding())
            {
                if (shown++ == 64)
                {
                    report.Note("And " + (outstanding - 64) + " more.");
                    break;
                }
                report.Field(status.ContentId.ToString("D").Substring(0, 8) + "  " + status.Kind, status.State.ToString(), status.State == RequestState.Ready ? MonitorTone.Normal : MonitorTone.Caution);
            }
            var history = Provider.GetHistory();
            int failed = 0;
            foreach (var status in history)
                if (status.State == RequestState.Failed || status.State == RequestState.CleanupFailed)
                    failed++;
            report.Section("Recent");
            report.Field("Completed", history.Length);
            report.Field("Failed", failed, failed > 0 ? MonitorTone.Bad : MonitorTone.Muted);
        }
    }
}
