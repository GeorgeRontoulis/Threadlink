namespace Threadforge.Threadlink
{
    /// <summary>
    /// Vault: definitions for the session. Designers author <see cref="VaultAsset{T}"/> assets; code asks Vault for
    /// their prepared, immutable values, prepared once and shared for the whole session.
    /// </summary>
    public sealed class Vault : ThreadlinkSubsystem<Vault>
    {
        private VaultContext context;

        /// <inheritdoc />
        protected override void OnSubsystemBoot() => context = new VaultContext();

        /// <inheritdoc />
        protected override void OnSubsystemShutdown() => context = null;

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(context.PreparedCount + " prepared");
            if (report.Detailed)
                report.Section("Vault").Field("Prepared this session", context.PreparedCount);
        }

        /// <summary>The prepared value of a definition. Preparation validates the schema, variants and references once.</summary>
        public static T Get<T>(IVaultSource<T> definition) => Instance.context.Prepare(definition).Value;

        /// <summary>The prepared definition with its identity, schema and version.</summary>
        public static VaultRecord<T> Prepare<T>(IVaultSource<T> definition) => Instance.context.Prepare(definition);
    }
}
