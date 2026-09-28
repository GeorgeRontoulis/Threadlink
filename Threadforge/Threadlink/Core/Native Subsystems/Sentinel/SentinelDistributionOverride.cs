namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    ///LEAVE FULLY QUALIFIED NAMESPACES HERE FOR UNITY'S ROSLYN ANALYZERS TO INTERCEPT THEM PROPERLY IN CODEGEN.
    using global::Threadlink.Core.NativeSubsystems.Scribe;
    using System;
    using Unity.Scripting.LifecycleManagement;

    /// <summary>
    /// Runtime override of the distribution resolved from <see cref="SentinelConfig"/>.
    ///
    /// Intended for instances that must not deploy the configured ecosystem, such as several
    /// development instances on one machine that cannot share a single Steam account.
    ///
    /// Sources, in order of precedence:
    /// <list type="number">
    /// <item><see cref="Requested"/>, assigned by project code before Threadlink deploys.</item>
    /// <item>The <c>-threadlink-sentinel-distribution &lt;Distribution&gt;</c> command-line argument.</item>
    /// <item>The <c>THREADLINK_SENTINEL_DISTRIBUTION</c> environment variable.</item>
    /// </list>
    /// The command-line and environment sources are honoured in the Editor and in development builds only.
    /// An override must still be allowed for the active platform by <see cref="SentinelDistributionPolicy"/>.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class SentinelDistributionOverride
    {
        public const string COMMAND_LINE_ARGUMENT = "-threadlink-sentinel-distribution";
        public const string ENVIRONMENT_VARIABLE = "THREADLINK_SENTINEL_DISTRIBUTION";

        /// <summary>
        /// The distribution requested by project code. Assign it before Threadlink deploys,
        /// for example from <see cref="UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad"/>.
        /// <see cref="SentinelDistribution.None"/> defers to the next source.
        /// </summary>
        public static SentinelDistribution Requested { get; set; } = SentinelDistribution.None;

        /// <summary>
        /// Attempt to resolve a distribution requested by any of the override sources.
        /// </summary>
        /// <param name="distribution">The requested distribution.</param>
        /// <param name="source">The source that requested it.</param>
        /// <returns><see langword="true"/> if a source requested a distribution. <see langword="false"/> otherwise.</returns>
        internal static bool TryResolve(out SentinelDistribution distribution, out string source)
        {
            if (Requested is not SentinelDistribution.None)
            {
                distribution = Requested;
                source = nameof(SentinelDistributionOverride) + "." + nameof(Requested);
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryParse(ReadCommandLineArgument(), COMMAND_LINE_ARGUMENT, out distribution))
            {
                source = COMMAND_LINE_ARGUMENT;
                return true;
            }

            if (TryParse(Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE), ENVIRONMENT_VARIABLE, out distribution))
            {
                source = ENVIRONMENT_VARIABLE;
                return true;
            }
#endif

            distribution = SentinelDistribution.None;
            source = null;
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ReadCommandLineArgument()
        {
            var arguments = Environment.GetCommandLineArgs();
            int lastIndex = arguments.Length - 1;

            for (int i = 0; i < lastIndex; i++)
            {
                if (string.Equals(arguments[i], COMMAND_LINE_ARGUMENT, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            }

            return null;
        }

        private static bool TryParse(string value, string source, out SentinelDistribution distribution)
        {
            distribution = SentinelDistribution.None;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (Enum.TryParse(value.Trim(), true, out distribution)
            && distribution is not SentinelDistribution.None
            && Enum.IsDefined(typeof(SentinelDistribution), distribution))
            {
                return true;
            }

            Scribe.Send<Sentinel>("Ignoring unknown distribution '", value, "' requested by ", source, ".")
                .ToUnityConsole(DebugType.Warning);

            distribution = SentinelDistribution.None;
            return false;
        }
#endif
    }
}
