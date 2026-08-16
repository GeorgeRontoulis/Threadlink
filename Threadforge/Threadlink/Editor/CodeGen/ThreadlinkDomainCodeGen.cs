namespace Threadlink.Editor.CodeGen
{
    using CSharpier;
    using Cysharp.Text;
    using System;
    using System.Collections.Generic;
    using Threadlink.Core.NativeSubsystems.Scribe;

    internal static class ThreadlinkDomainCodeGen
    {
        private static readonly List<ThreadlinkDomainEntry> EntryBuffer = new(16);
        private static readonly HashSet<string> LiveKeys = new(16, StringComparer.Ordinal);

        internal static bool TryGenerate(ThreadlinkDomainDescriptor descriptor, out ThreadlinkDomainDiff diff)
        {
            diff = new ThreadlinkDomainDiff(descriptor.DomainName);

            if (descriptor.TryValidate(out var failureReason) is false)
            {
                Scribe.Send<ThreadlinkDomainDescriptor>("Domain '", descriptor.DomainName, "' was skipped because ",
                failureReason, ".").ToUnityConsole(DebugType.Error);
                return false;
            }

            var manifest = ThreadlinkDomainManifest.LoadOrCreate(descriptor);

            ThreadlinkDomainSources.Gather(descriptor, EntryBuffer);

            LiveKeys.Clear();

            int entryCount = EntryBuffer.Count;

            if (entryCount <= 0)
            {
                Scribe.Send<ThreadlinkDomainDescriptor>("Domain '", descriptor.DomainName, "' produced no entries from ",
                descriptor.Sources.Count, " source file(s). Only its reserved members and any surviving tombstones will be ",
                "emitted. If this is unexpected, check that its definition files still exist and are assigned.")
                .ToUnityConsole(DebugType.Warning);
            }

            for (int i = 0; i < entryCount; i++)
            {
                var entry = EntryBuffer[i];

                if (TryResolveEntry(entry, descriptor, manifest, diff) is false)
                    return false;

                LiveKeys.Add(entry.Key);
            }

            ApplyTombstones(descriptor, manifest, diff);

            if (diff.HasBlockingIssues)
                return false;

            if (TryEmit(descriptor, manifest) is false)
                return false;

            manifest.Save(descriptor);
            return true;
        }

        private static bool TryResolveEntry(ThreadlinkDomainEntry entry, ThreadlinkDomainDescriptor descriptor,
        ThreadlinkDomainManifest manifest, ThreadlinkDomainDiff diff)
        {
            bool existed = manifest.TryGetByKey(entry.Key, out var record);

            if (existed && record.tombstoned is false)
            {
                if (ThreadlinkDomainAllocator.ScopeChanged(record, entry))
                {
                    diff.Record(ThreadlinkDomainChangeKind.Rescoped, record.name,
                    $"moved from scope '{record.scope}' to '{entry.Scope}'. The manifest pins the original value, so nothing breaks");

                    record.scope = entry.Scope;
                }

                return true;
            }

            if (ThreadlinkDomainAllocator.TryAllocateName(entry, manifest, diff, out var name) is false)
                return false;

            int value;
            int seedOffset = 0;

            if (descriptor.Kind is ThreadlinkDomainKind.Identity)
            {
                if (ThreadlinkDomainAllocator.TryAllocateIdentityValue(entry, descriptor, manifest, diff, out value, out seedOffset) is false)
                    return false;
            }
            else value = ThreadlinkDomainAllocator.AllocateOrdinalValue(descriptor, manifest);

            manifest.Register(new ThreadlinkDomainManifestEntry
            {
                key = entry.Key,
                name = name,
                scope = entry.Scope,
                value = value,
                seedOffset = seedOffset,
                tombstoned = false
            });

            diff.Record(existed ? ThreadlinkDomainChangeKind.Renamed : ThreadlinkDomainChangeKind.Added, name,
            existed ? $"revived from a tombstone at value {value}" : $"value {value}");

            return true;
        }

        private static void ApplyTombstones(ThreadlinkDomainDescriptor descriptor, ThreadlinkDomainManifest manifest,
        ThreadlinkDomainDiff diff)
        {
            for (int i = manifest.entries.Count - 1; i >= 0; i--)
            {
                var record = manifest.entries[i];

                if (record.tombstoned || LiveKeys.Contains(record.key))
                    continue;

                if (descriptor.TombstoneRemovedEntries)
                {
                    record.tombstoned = true;

                    diff.Record(ThreadlinkDomainChangeKind.Removed, record.name,
                    "retained as an obsolete tombstone so serialized references stay resolvable");
                }
                else
                {
                    manifest.entries.RemoveAt(i);

                    diff.Record(ThreadlinkDomainChangeKind.Removed, record.name,
                    "dropped entirely. Any code referencing it will now fail to compile");
                }
            }

            manifest.Reindex();
        }

        private static bool TryEmit(ThreadlinkDomainDescriptor descriptor, ThreadlinkDomainManifest manifest)
        {
            var ordered = new List<ThreadlinkDomainManifestEntry>(manifest.entries);

            ordered.Sort(static (a, b) => a.value.CompareTo(b.value));

            using var builder = ZString.CreateStringBuilder();

            int count = ordered.Count;

            for (int i = 0; i < count; i++)
            {
                var record = ordered[i];

                if (i > 0)
                    builder.AppendLine();

                if (record.tombstoned)
                {
                    builder.Append("[System.Obsolete(\"Removed from the domain definition. Retained so previously serialized data stays resolvable.\")]");
                    builder.AppendLine();
                }

                builder.Append(record.name);
                builder.Append(" = ");
                builder.Append(record.value);
                builder.Append(',');
            }

            try
            {
                var merged = descriptor.ShellText.Replace(ThreadlinkDomainDescriptor.ENTRIES_TOKEN, builder.ToString());

                ThreadlinkAssetIO.WriteText(descriptor.OutputAssetPath, CodeFormatter.Format(merged).Code);
                return true;
            }
            catch (Exception exception)
            {
                Scribe.Send<ThreadlinkDomainDescriptor>("Failed to emit domain '", descriptor.DomainName, "': ",
                exception.Message).ToUnityConsole(DebugType.Error);
                return false;
            }
        }
    }
}
