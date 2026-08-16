namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Shared;

    internal static class ThreadlinkDomainAllocator
    {
        private const int MAX_REHASH_ATTEMPTS = 64;

        internal static bool TryAllocateName(ThreadlinkDomainEntry entry, ThreadlinkDomainManifest manifest,
        ThreadlinkDomainDiff diff, out string result)
        {
            var preferred = EnumCodeGen.SanitizeEnumName(entry.PreferredName);

            if (manifest.IsNameTaken(preferred, entry.Key) is false)
            {
                result = preferred;
                return true;
            }

            if (string.IsNullOrEmpty(entry.Scope) is false)
            {
                var qualified = EnumCodeGen.SanitizeEnumName(string.Concat(entry.Scope, "_", preferred));

                if (manifest.IsNameTaken(qualified, entry.Key) is false)
                {
                    result = qualified;
                    return true;
                }
            }

            for (int suffix = 2; suffix < MAX_REHASH_ATTEMPTS; suffix++)
            {
                var candidate = string.Concat(preferred, "_", suffix.ToString());

                if (manifest.IsNameTaken(candidate, entry.Key) is false)
                {
                    result = candidate;
                    return true;
                }
            }

            diff.Record(ThreadlinkDomainChangeKind.Collision, preferred,
            $"could not resolve a unique member name for key '{entry.Key}'");

            result = null;
            return false;
        }

        internal static bool TryAllocateIdentityValue(ThreadlinkDomainEntry entry, ThreadlinkDomainDescriptor descriptor,
        ThreadlinkDomainManifest manifest, ThreadlinkDomainDiff diff, out int value, out int seedOffset)
        {
            for (seedOffset = 0; seedOffset < MAX_REHASH_ATTEMPTS; seedOffset++)
            {
                value = seedOffset is 0
                ? HashFunctions.ToXxHash32(entry.Key)
                : HashFunctions.ToXxHash32(entry.Key, seedOffset);

                if (descriptor.ReservedValues.Contains(value))
                    continue;

                if (manifest.IsValueTaken(value, entry.Key))
                    continue;

                return true;
            }

            diff.Record(ThreadlinkDomainChangeKind.Collision, entry.Key,
            $"hash could not be resolved after {MAX_REHASH_ATTEMPTS} attempts");

            value = 0;
            seedOffset = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AllocateOrdinalValue(ThreadlinkDomainDescriptor descriptor, ThreadlinkDomainManifest manifest)
        {
            int candidate = manifest.nextOrdinal;

            while (descriptor.ReservedValues.Contains(candidate) || manifest.IsValueTaken(candidate, string.Empty))
                candidate++;

            manifest.nextOrdinal = candidate + 1;
            return candidate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ScopeChanged(ThreadlinkDomainManifestEntry existing, ThreadlinkDomainEntry incoming)
        {
            return string.Equals(existing.scope, incoming.Scope, StringComparison.Ordinal) is false;
        }
    }
}
