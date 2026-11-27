namespace Threadforge.Threadlink
{
    public sealed partial class Threadlink
    {
        private readonly OperationLog operations = new OperationLog(DiagnosticsOptions.Development());

        /// <summary>The session's operation log: outcomes, stages and history, shown in the Runtime monitor.</summary>
        public static OperationLog Operations => Instance.operations;

        /// <summary>Starts recording an operation. Record its outcome on the returned scope; disposing without one records it as abandoned.</summary>
        /// <example><code>
        /// var save = Threadlink.BeginOperation("Saves", "Autosave");
        /// save.Stage("Writing slot 0");
        /// save.Succeed();
        /// </code></example>
        public static OperationScope BeginOperation(string category, string name) => Instance.operations.Begin(category, name);

        private void ReportOperations(MonitorReport report)
        {
            report.Section("Operations");
            report.Field("Started", operations.Started);
            report.Field("Running", operations.Count(OperationOutcome.Running));
            report.Field("Failed", operations.Count(OperationOutcome.Failed), operations.Count(OperationOutcome.Failed) > 0 ? MonitorTone.Bad : MonitorTone.Muted);
            var history = operations.GetHistory();
            for (int i = history.Length - 1, shown = 0; i >= 0 && shown < 12; i--, shown++)
            {
                var record = history[i];
                report.Field(
                    record.Category + " · " + record.Name,
                    record.Outcome + (record.Detail == null ? string.Empty : ": " + record.Detail),
                    record.Outcome == OperationOutcome.Succeeded ? MonitorTone.Muted
                    : record.Outcome == OperationOutcome.Failed ? MonitorTone.Bad
                    : MonitorTone.Caution
                );
            }
        }
    }
}
