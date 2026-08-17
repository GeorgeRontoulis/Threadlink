namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.Shared;
    using Threadlink.Utilities.Strings;
    using UnityEditor;

    internal static class ThreadlinkDomainRegistry
    {
        internal const string SCENES_DOMAIN = "Addressables.Scenes";
        internal const string ASSETS_DOMAIN = "Addressables.Assets";
        internal const string PREFABS_DOMAIN = "Addressables.Prefabs";

        private const string MANIFESTS_SUBFOLDER = "Manifests";

        private static readonly HashSet<string> ClaimedInjectors = new(16, StringComparer.Ordinal);

        internal static bool TryBuildAll(ThreadlinkEditorConfig config, List<ThreadlinkDomainDescriptor> buffer)
        {
            buffer.Clear();
            ClaimedInjectors.Clear();

            if (config == null)
                return false;

            var generatedFolder = ResolveAssetPath(config.GeneratedScriptsFolder);

            if (string.IsNullOrEmpty(generatedFolder))
            {
                Scribe.Send<ThreadlinkEditorConfig>("The Generated Scripts Folder is not assigned.").ToUnityConsole(DebugType.Error);
                return false;
            }

            var injectorsFolder = ResolveAbsolutePath(config.InjectorsFolder);
            var addressablesFolder = ResolveAbsolutePath(config.AddressablesInjectorsFolder);

            var domains = config.NativeDomains;
            int domainCount = domains.Length;

            for (int i = 0; i < domainCount; i++)
                AddNativeDomain(buffer, domains[i], generatedFolder, injectorsFolder, addressablesFolder);

            AddDefinedDomains(buffer, config, injectorsFolder);

            ReportOrphanedInjectors(injectorsFolder);

            return WarnOnDuplicateOutputs(buffer) && buffer.Count > 0;
        }

        internal static void CollectWatchedPaths(ThreadlinkEditorConfig config, HashSet<string> buffer)
        {
            buffer.Clear();

            if (config == null)
                return;

            var domains = config.NativeDomains;
            int domainCount = domains.Length;

            for (int i = 0; i < domainCount; i++)
            {
                var domain = domains[i];

                if (domain != null && domain.NativeEntries != null)
                    buffer.Add(AssetDatabase.GetAssetPath(domain.NativeEntries));
            }

            AddFolder(buffer, config.InjectorsFolder);
            AddFolder(buffer, config.AddressablesInjectorsFolder);
            AddFolder(buffer, config.DomainDefinitionsFolder);
        }

        private static void AddNativeDomain(List<ThreadlinkDomainDescriptor> buffer, ThreadlinkDomainAssets assets,
        string generatedFolder, string injectorsFolder, string addressablesFolder)
        {
            if (assets == null || string.IsNullOrEmpty(assets.DomainName))
                return;

            if (assets.Shell == null)
            {
                Scribe.Send<ThreadlinkEditorConfig>("Domain '", assets.DomainName, "' has no shell template assigned. ",
                "It will not be generated.").ToUnityConsole(DebugType.Error);
                return;
            }

            var descriptor = new ThreadlinkDomainDescriptor
            {
                DomainName = assets.DomainName,
                Kind = assets.IsOrdinal ? ThreadlinkDomainKind.Ordinal : ThreadlinkDomainKind.Identity,
                ShellText = assets.Shell.text,
                OutputRootFolder = generatedFolder,
                OutputAssetPath = $"{generatedFolder}/{ResolveFileName(assets)}.cs",
                ManifestAssetPath = $"{generatedFolder}/{MANIFESTS_SUBFOLDER}/{assets.DomainName}.manifest.json",
                TombstoneRemovedEntries = assets.IsOrdinal is false
            };

            if (assets.ReserveZero)
                descriptor.Reserve(0);

            if (assets.NativeEntries != null)
                descriptor.AddSource(ResolveAbsolutePath(assets.NativeEntries), string.Empty);

            AddInjectors(descriptor, injectorsFolder, assets.DomainName);

            if (assets.SourcedFromAddressables)
                AddInjectors(descriptor, addressablesFolder, assets.DomainName);

            buffer.Add(descriptor);
        }

        private static void AddDefinedDomains(List<ThreadlinkDomainDescriptor> buffer, ThreadlinkEditorConfig config,
        string injectorsFolder)
        {
            if (config.DomainDefinitionShell == null)
                return;

            var definitionsFolder = ResolveAbsolutePath(config.DomainDefinitionsFolder);
            var scriptsFolder = ResolveAssetPath(config.DomainDefinitionScriptsFolder);

            if (string.IsNullOrEmpty(definitionsFolder) || string.IsNullOrEmpty(scriptsFolder)
            || Directory.Exists(definitionsFolder) is false)
            {
                return;
            }

            var files = Directory.GetFiles(definitionsFolder, "*.txt", SearchOption.TopDirectoryOnly);

            Array.Sort(files, StringComparer.Ordinal);

            int length = files.Length;
            var shellText = config.DomainDefinitionShell.text;

            for (int i = 0; i < length; i++)
            {
                var domainName = Path.GetFileNameWithoutExtension(files[i]);

                var descriptor = new ThreadlinkDomainDescriptor
                {
                    DomainName = domainName,
                    Kind = ThreadlinkDomainKind.Identity,
                    ShellText = shellText.Replace(ThreadlinkDomainDescriptor.NAME_TOKEN, domainName),
                    OutputRootFolder = scriptsFolder,
                    OutputAssetPath = $"{scriptsFolder}/{domainName}.cs",
                    ManifestAssetPath = $"{scriptsFolder}/{MANIFESTS_SUBFOLDER}/{domainName}.manifest.json",
                    TombstoneRemovedEntries = true
                };

                descriptor.Reserve(0);
                descriptor.AddSource(files[i], string.Empty);

                AddInjectors(descriptor, injectorsFolder, domainName);

                buffer.Add(descriptor);
            }
        }

        private static void AddInjectors(ThreadlinkDomainDescriptor descriptor, string folder, string domainName)
        {
            if (string.IsNullOrEmpty(folder) || Directory.Exists(folder) is false)
                return;

            var candidates = Directory.GetFiles(folder, $"{domainName}.*.txt", SearchOption.AllDirectories);

            Array.Sort(candidates, StringComparer.Ordinal);

            int length = candidates.Length;

            for (int i = 0; i < length; i++)
            {
                var path = candidates[i];

                if (TryResolveInjectorScope(path, domainName, out var scope) is false)
                    continue;

                ClaimedInjectors.Add(path);
                descriptor.AddSource(path, scope);
            }
        }

        private static bool TryResolveInjectorScope(string path, string domainName, out string scope)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);

            scope = null;

            if (fileName.Length <= domainName.Length + 1)
                return false;

            if (fileName[domainName.Length] is not '.')
                return false;

            var remainder = fileName[(domainName.Length + 1)..];

            if (remainder.IndexOf('.') >= 0)
            {
                Scribe.Send<ThreadlinkDomainDescriptor>("Injector '", fileName, "' has more than one segment after '",
                domainName, "'. Injector files must be named {DomainName}.{Injector}.txt")
                .ToUnityConsole(DebugType.Warning);
                return false;
            }

            scope = remainder;
            return true;
        }

        private static void ReportOrphanedInjectors(string injectorsFolder)
        {
            if (string.IsNullOrEmpty(injectorsFolder) || Directory.Exists(injectorsFolder) is false)
                return;

            var files = Directory.GetFiles(injectorsFolder, "*.txt", SearchOption.AllDirectories);
            int length = files.Length;

            for (int i = 0; i < length; i++)
            {
                if (ClaimedInjectors.Contains(files[i]))
                    continue;

                Scribe.Send<ThreadlinkDomainDescriptor>("Injector '", Path.GetFileName(files[i]),
                "' matches no known domain and was ignored. Check that its prefix matches a domain name exactly.")
                .ToUnityConsole(DebugType.Warning);
            }
        }

        private static bool WarnOnDuplicateOutputs(List<ThreadlinkDomainDescriptor> buffer)
        {
            var seen = new Dictionary<string, string>(buffer.Count, StringComparer.OrdinalIgnoreCase);
            bool valid = true;

            int count = buffer.Count;

            for (int i = 0; i < count; i++)
            {
                var descriptor = buffer[i];

                if (seen.TryGetValue(descriptor.OutputAssetPath, out var owner))
                {
                    Scribe.Send<ThreadlinkEditorConfig>("Domains '", owner, "' and '", descriptor.DomainName, "' both target '",
                    descriptor.OutputAssetPath, "'.").ToUnityConsole(DebugType.Error);
                    valid = false;
                    continue;
                }

                seen[descriptor.OutputAssetPath] = descriptor.DomainName;
            }

            return valid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ResolveFileName(ThreadlinkDomainAssets assets)
        {
            return string.IsNullOrEmpty(assets.OutputFileName) ? assets.DomainName : assets.OutputFileName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddFolder(HashSet<string> buffer, DefaultAsset folder)
        {
            if (folder != null)
                buffer.Add(AssetDatabase.GetAssetPath(folder));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ResolveAssetPath(DefaultAsset folder)
        {
            return folder == null ? string.Empty : AssetDatabase.GetAssetPath(folder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ResolveAbsolutePath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset).ToAbsolutePath();
        }
    }

    internal class ThreadlinkDomainCodeGenRunner
    {
        private static readonly List<ThreadlinkDomainDescriptor> Descriptors = new(12);

        [MenuItem("Threadlink/CodeGen/Run Domain CodeGen")]
        internal static void Run()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
            {
                Scribe.Send<ThreadlinkDomainCodeGenRunner>("Threadlink Editor Config not found. Domain CodeGen aborted.")
                .ToUnityConsole(DebugType.Error);
                return;
            }

            if (ThreadlinkDomainRegistry.TryBuildAll(editorConfig, Descriptors) is false)
            {
                Scribe.Send<ThreadlinkDomainCodeGenRunner>("Domain descriptors could not be built. Domain CodeGen aborted.")
                .ToUnityConsole(DebugType.Error);
                Descriptors.Clear();
                return;
            }

            bool anyChange = false;
            bool anyFailure = false;

            int count = Descriptors.Count;

            for (int i = 0; i < count; i++)
            {
                if (ThreadlinkDomainCodeGen.TryGenerate(Descriptors[i], out var diff) is false)
                    anyFailure = true;

                if (diff.HasChanges())
                {
                    diff.Report();
                    anyChange = true;
                }
            }

            Descriptors.Clear();

            if (anyChange)
                AssetDatabase.Refresh();

            if (anyFailure)
                Scribe.Send<ThreadlinkDomainCodeGenRunner>("Domain CodeGen finished with errors. Failed domains were left untouched.")
                .ToUnityConsole(DebugType.Error);
        }
    }

    internal sealed class ThreadlinkDomainImporter : AssetPostprocessor
    {
        private static readonly HashSet<string> WatchedPaths = new(8, StringComparer.Ordinal);

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig config))
                return;

            ThreadlinkDomainRegistry.CollectWatchedPaths(config, WatchedPaths);

            if (WatchedPaths.Count <= 0)
                return;

            if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))
                ThreadlinkDomainCodeGenRunner.Run();
        }

        private static bool Touches(string[] assetPaths)
        {
            int length = assetPaths.Length;

            for (int i = 0; i < length; i++)
            {
                var path = assetPaths[i];

                if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) is false)
                    continue;

                foreach (var watched in WatchedPaths)
                {
                    if (path.StartsWith(watched, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }
    }
}
