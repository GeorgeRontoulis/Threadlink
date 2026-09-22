namespace Threadlink.Editor.Sentinel
{
    using Core.NativeSubsystems.Sentinel;
    using System.Collections.Generic;
    using UnityEditor.Build.Reporting;

    public enum SentinelBuildDiagnosticSeverity : byte
    {
        Info = 0,
        Warning,
        Error
    }

    public readonly struct SentinelBuildDiagnostic
    {
        public SentinelBuildDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public SentinelBuildDiagnostic(
            SentinelBuildDiagnosticSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message;
        }

        public static SentinelBuildDiagnostic Info(string message) =>
            new(SentinelBuildDiagnosticSeverity.Info, message);

        public static SentinelBuildDiagnostic Warning(string message) =>
            new(SentinelBuildDiagnosticSeverity.Warning, message);

        public static SentinelBuildDiagnostic Error(string message) =>
            new(SentinelBuildDiagnosticSeverity.Error, message);
    }

    public readonly struct SentinelBuildContext
    {
        public BuildReport Report { get; }
        public SentinelModuleKey Key { get; }

        public string OutputPath => Report.summary.outputPath;

        internal SentinelBuildContext(
            BuildReport report,
            SentinelModuleKey key)
        {
            Report = report;
            Key = key;
        }

        public T GetSubtarget<T>() where T : System.Enum =>
            Report.summary.GetSubtarget<T>();
    }

    /// <summary>
    /// Editor-side integration for one or more exact Sentinel module keys.
    ///
    /// There is no ranking and no fallback competition here. Build selection first
    /// resolves the exact Platform / Distribution key, then requires exactly one
    /// installed build module to claim that key.
    /// </summary>
    public interface ISentinelBuildModule
    {
        string ModuleID { get; }
        string DisplayName { get; }

        bool Implements(in SentinelModuleKey key);

        void ConfigureBuild(
            in SentinelBuildContext context,
            List<SentinelBuildDiagnostic> diagnostics);

        void ValidateBuild(
            in SentinelBuildContext context,
            List<SentinelBuildDiagnostic> diagnostics);
    }
}
