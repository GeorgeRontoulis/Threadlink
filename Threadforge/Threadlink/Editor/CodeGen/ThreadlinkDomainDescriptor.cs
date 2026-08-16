namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal enum ThreadlinkDomainKind : byte
    {
        Identity,
        Ordinal
    }

    internal sealed class ThreadlinkDomainSource
    {
        internal string AbsolutePath { get; }
        internal string Scope { get; }

        internal ThreadlinkDomainSource(string absolutePath, string scope)
        {
            AbsolutePath = absolutePath;
            Scope = scope;
        }
    }

    internal sealed class ThreadlinkDomainEntry
    {
        internal string Key { get; }
        internal string PreferredName { get; }
        internal string Scope { get; }

        internal ThreadlinkDomainEntry(string key, string preferredName, string scope)
        {
            Key = key;
            PreferredName = preferredName;
            Scope = scope;
        }
    }

    internal sealed class ThreadlinkDomainDescriptor
    {
        internal const string ENTRIES_TOKEN = "{DOMAIN_ENTRIES}";
        internal const string NAME_TOKEN = "{DOMAIN_NAME}";

        internal string DomainName { get; set; }
        internal ThreadlinkDomainKind Kind { get; set; }
        internal string ShellText { get; set; }
        internal string OutputAssetPath { get; set; }
        internal string ManifestAssetPath { get; set; }
        internal string OutputRootFolder { get; set; }
        internal bool TombstoneRemovedEntries { get; set; }
        internal HashSet<int> ReservedValues { get; } = new(1);
        internal List<ThreadlinkDomainSource> Sources { get; } = new(3);

        internal bool TryValidate(out string failureReason)
        {
            if (string.IsNullOrEmpty(DomainName))
            {
                failureReason = "the domain has no name";
                return false;
            }

            if (string.IsNullOrEmpty(ShellText))
            {
                failureReason = "no shell template is assigned";
                return false;
            }

            if (ShellText.Contains(ENTRIES_TOKEN, StringComparison.Ordinal) is false)
            {
                failureReason = $"the shell template has no {ENTRIES_TOKEN} token";
                return false;
            }

            if (string.IsNullOrEmpty(OutputAssetPath) || string.IsNullOrEmpty(ManifestAssetPath))
            {
                failureReason = "the output or manifest path could not be resolved";
                return false;
            }

            if (IsContained(OutputAssetPath) is false)
            {
                failureReason = $"the output path '{OutputAssetPath}' falls outside '{OutputRootFolder}'";
                return false;
            }

            failureReason = null;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsContained(string assetPath)
        {
            return string.IsNullOrEmpty(OutputRootFolder) is false
            && assetPath.StartsWith(OutputRootFolder, StringComparison.Ordinal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ThreadlinkDomainDescriptor Reserve(params int[] values)
        {
            int length = values.Length;

            for (int i = 0; i < length; i++)
                ReservedValues.Add(values[i]);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ThreadlinkDomainDescriptor AddSource(string absolutePath, string scope)
        {
            if (string.IsNullOrEmpty(absolutePath) is false)
                Sources.Add(new ThreadlinkDomainSource(absolutePath, scope));

            return this;
        }
    }
}