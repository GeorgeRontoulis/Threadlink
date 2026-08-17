namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.Utilities.Strings;
    using UnityEngine;

    [Serializable]
    internal sealed class ThreadlinkDomainManifestEntry
    {
        public string key;
        public string name;
        public string scope;
        public int value;
        public int seedOffset;
        public bool tombstoned;
    }

    [Serializable]
    internal sealed class ThreadlinkDomainManifest
    {
        public string domain;
        public string kind;
        public List<ThreadlinkDomainManifestEntry> entries = new();

        [NonSerialized] private Dictionary<string, ThreadlinkDomainManifestEntry> byKey;
        [NonSerialized] private Dictionary<string, ThreadlinkDomainManifestEntry> byName;

        internal void Reindex()
        {
            int count = entries.Count;

            byKey = new(count, StringComparer.Ordinal);
            byName = new(count, StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];

                byKey[entry.key] = entry;
                byName[entry.name] = entry;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetByKey(string key, out ThreadlinkDomainManifestEntry result) => byKey.TryGetValue(key, out result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNameTaken(string name, string byOtherKeyThan)
        {
            return byName.TryGetValue(name, out var existing)
            && string.Equals(existing.key, byOtherKeyThan, StringComparison.Ordinal) is false;
        }

        internal bool IsValueTaken(int value, string byOtherKeyThan)
        {
            int count = entries.Count;

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];

                if (entry.value == value && string.Equals(entry.key, byOtherKeyThan, StringComparison.Ordinal) is false)
                    return true;
            }

            return false;
        }

        internal void Register(ThreadlinkDomainManifestEntry entry)
        {
            if (byKey.TryGetValue(entry.key, out var existing))
            {
                byName.Remove(existing.name);
                entries.Remove(existing);
            }

            entries.Add(entry);
            byKey[entry.key] = entry;
            byName[entry.name] = entry;
        }

        internal static ThreadlinkDomainManifest LoadOrCreate(ThreadlinkDomainDescriptor descriptor)
        {
            var path = descriptor.ManifestAssetPath.ToAbsolutePath();

            ThreadlinkDomainManifest manifest = null;

            if (File.Exists(path))
            {
                try
                {
                    manifest = JsonUtility.FromJson<ThreadlinkDomainManifest>(File.ReadAllText(path));
                }
                catch (Exception exception)
                {
                    Scribe.Send<ThreadlinkDomainManifest>("Manifest for domain '", descriptor.DomainName,
                    "' is corrupt and will be rebuilt: ", exception.Message).ToUnityConsole(DebugType.Error);
                    manifest = null;
                }
            }

            manifest ??= new ThreadlinkDomainManifest();
            manifest.entries ??= new();
            manifest.domain = descriptor.DomainName;
            manifest.kind = descriptor.Kind.ToString();

            manifest.Reindex();
            return manifest;
        }

        internal void Save(ThreadlinkDomainDescriptor descriptor)
        {
            entries.Sort(static (a, b) => string.CompareOrdinal(a.key, b.key));

            ThreadlinkAssetIO.WriteText(descriptor.ManifestAssetPath, JsonUtility.ToJson(this, true));
        }
    }
}
