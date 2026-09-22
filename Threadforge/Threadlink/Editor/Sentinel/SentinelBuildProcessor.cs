namespace Threadlink.Editor.Sentinel
{
    using Core.NativeSubsystems.Sentinel;
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;

    /// <summary>
    /// Resolves Sentinel deterministically from:
    ///
    /// Unity BuildTarget -> SentinelPlatform
    /// SentinelConfig    -> SentinelDistribution (only when target is ambiguous)
    /// exact key         -> exactly one installed ISentinelBuildModule
    /// </summary>
    internal sealed class SentinelBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var platform = SentinelBuildPlatformResolver.Resolve(report.summary.platform);

            if (platform is SentinelPlatformMarker.Unknown)
            {
                throw new BuildFailedException($"Sentinel: unsupported Unity build target '{report.summary.platform}'.");
            }

            if (!SentinelEditorConfigResolver.TryGetSentinelConfig(out var config, out string configError))
            {
                throw new BuildFailedException($"Sentinel: {configError}");
            }

            if (!SentinelEditorConfigResolver.ValidateNativeConfigReference(config, out configError))
            {
                throw new BuildFailedException($"Sentinel: {configError}");
            }

            var distribution = config.GetDistribution(platform);

            if (distribution is SentinelDistribution.None)
            {
                throw new BuildFailedException($"Sentinel: SentinelConfig contains an invalid distribution for '{platform}'.");
            }

            var key = new SentinelModuleKey(platform, distribution);
            var module = ResolveExactBuildModule(key);
            var context = new SentinelBuildContext(report, key);
            var diagnostics = new List<SentinelBuildDiagnostic>(8);

            try
            {
                module.ConfigureBuild(context, diagnostics);
                module.ValidateBuild(context, diagnostics);
            }
            catch (Exception exception)
            {
                throw new BuildFailedException($"Sentinel: module '{module.ModuleID}' threw while preparing '{key}':\n{exception}");
            }

            bool hasErrors = false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                string message = $"Sentinel [{module.DisplayName}] [{key}]: {diagnostic.Message}";

                switch (diagnostic.Severity)
                {
                    case SentinelBuildDiagnosticSeverity.Error:
                        hasErrors = true;
                        Debug.LogError(message);
                        break;

                    case SentinelBuildDiagnosticSeverity.Warning:
                        Debug.LogWarning(message);
                        break;

                    default:
                        Debug.Log(message);
                        break;
                }
            }

            if (hasErrors)
            {
                throw new BuildFailedException($"Sentinel: build validation failed for '{key}'. "
                + "See the Sentinel diagnostics above.");
            }

            Debug.Log($"Sentinel: '{report.summary.platform}' resolved exactly to '{key}' "
            + $"through '{module.DisplayName}' [{module.ModuleID}].");
        }

        private static ISentinelBuildModule ResolveExactBuildModule(SentinelModuleKey key)
        {
            ISentinelBuildModule selected = null;
            int matches = 0;

            var types = TypeCache.GetTypesDerivedFrom<ISentinelBuildModule>();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"Sentinel: build module '{type.FullName}' must expose a public parameterless constructor.");
                    continue;
                }

                if (Activator.CreateInstance(type) is not ISentinelBuildModule candidate)
                    continue;

                if (!candidate.Implements(key))
                    continue;

                selected = candidate;
                matches++;
            }

            if (matches == 0)
            {
                throw new BuildFailedException($"Sentinel: no installed build module implements exact key '{key}'. "
                + "Install the corresponding Sentinel platform module or change SentinelConfig.");
            }

            if (matches > 1)
            {
                throw new BuildFailedException($"Sentinel: {matches} build modules implement exact key '{key}'. "
                + "Each Platform / Distribution pair must have exactly one owner.");
            }

            return selected;
        }
    }
}
