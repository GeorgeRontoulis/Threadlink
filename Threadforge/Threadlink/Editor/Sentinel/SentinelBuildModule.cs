namespace Threadlink.Editor.Sentinel
{
    using Core.NativeSubsystems.Sentinel;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SentinelBuildDiagnostic(SentinelBuildDiagnosticSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SentinelBuildDiagnostic Info(string message) => new(SentinelBuildDiagnosticSeverity.Info, message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SentinelBuildDiagnostic Warning(string message) => new(SentinelBuildDiagnosticSeverity.Warning, message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SentinelBuildDiagnostic Error(string message) => new(SentinelBuildDiagnosticSeverity.Error, message);
    }

    public readonly struct SentinelBuildContext
    {
        public BuildReport Report { get; }
        public SentinelModuleKey Key { get; }

        public string OutputPath
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Report.summary.outputPath;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SentinelBuildContext(BuildReport report, SentinelModuleKey key)
        {
            Report = report;
            Key = key;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetSubtarget<T>() where T : System.Enum => Report.summary.GetSubtarget<T>();
    }

    /// <summary>
    /// Editor-side integration for one or more exact Sentinel module keys.
    /// </summary>
    public interface ISentinelBuildModule
    {
        string ModuleID { get; }
        string DisplayName { get; }

        bool Implements(in SentinelModuleKey key);
        void ConfigureBuild(in SentinelBuildContext context, List<SentinelBuildDiagnostic> diagnostics);
        void ValidateBuild(in SentinelBuildContext context, List<SentinelBuildDiagnostic> diagnostics);
    }
}
