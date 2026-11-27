namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>Final or current outcome of an observed operation.</summary>
    /// <remarks>Unsupported, unavailable, cancelled and failed are distinct outcomes; none is reported as success.</remarks>
    public enum OperationOutcome
    {
        /// <summary>The operation has not completed.</summary>
        Running,

        /// <summary>The operation completed successfully.</summary>
        Succeeded,

        /// <summary>The caller or owner cancelled the operation.</summary>
        Cancelled,

        /// <summary>The operation failed.</summary>
        Failed,

        /// <summary>The selected implementation does not support the requested capability.</summary>
        Unsupported,

        /// <summary>The capability is supported but currently unavailable.</summary>
        Unavailable,

        /// <summary>The owner disposed the scope without recording an outcome.</summary>
        Abandoned,
    }

    /// <summary>Recovery actions a game policy may choose after a failure. Diagnostics never performs them.</summary>
    [Flags]
    public enum RecoveryActions
    {
        /// <summary>No supported recovery action.</summary>
        None = 0,

        /// <summary>Repeating the same request is safe because nothing could have been committed.</summary>
        Retry = 1 << 0,

        /// <summary>Inspect the destination state before deciding whether to repeat work.</summary>
        Reconcile = 1 << 1,

        /// <summary>Select a retained earlier version, such as a save generation.</summary>
        RestorePrevious = 1 << 2,

        /// <summary>Choose between divergent versions.</summary>
        SelectVersion = 1 << 3,

        /// <summary>Sign in again or select another account.</summary>
        Reauthenticate = 1 << 4,

        /// <summary>Change configuration or authored data, then start a new operation.</summary>
        Reconfigure = 1 << 5,

        /// <summary>Discard the affected local data. Always requires explicit player or developer confirmation.</summary>
        Discard = 1 << 6,
    }

    /// <summary>Immutable trace entry with deferred formatting.</summary>
    public readonly struct TraceEntry
    {
        private readonly string template;
        private readonly object first,
            second;

        /// <summary>Operation identity, or zero for an unscoped trace.</summary>
        public long Operation { get; }

        /// <summary>Elapsed time since the log was created.</summary>
        public TimeSpan At { get; }

        internal TraceEntry(long operation, TimeSpan at, string template, object first, object second)
        {
            Operation = operation;
            At = at;
            this.template = template;
            this.first = first;
            this.second = second;
        }

        /// <summary>Formats the stored template only when requested.</summary>
        public override string ToString()
        {
            try
            {
                return string.Format(template ?? string.Empty, first, second);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }

    /// <summary>Snapshot of one observed operation.</summary>
    public sealed class OperationRecord
    {
        internal readonly object Gate = new object();
        internal long StartTimestamp;
        internal long EndTimestamp;
        internal long WaitStarted;
        private string stage,
            wait;
        private OperationOutcome outcome;
        private Exception error;
        private RecoveryActions recovery;
        private bool mayHaveCommitted;
        private string detail;

        /// <summary>Process-unique operation identity within its log.</summary>
        public long Id { get; }

        /// <summary>Owning subsystem or feature, for example "Persistence".</summary>
        public string Category { get; }

        /// <summary>Operation name, for example "Save slot 1".</summary>
        public string Name { get; }

        /// <summary>Most recently recorded stage.</summary>
        public string Stage
        {
            get
            {
                lock (Gate)
                    return stage;
            }
        }

        /// <summary>Known dependency or condition the operation currently waits on, if any.</summary>
        public string Waiting
        {
            get
            {
                lock (Gate)
                    return wait;
            }
        }

        /// <summary>Current or final outcome.</summary>
        public OperationOutcome Outcome
        {
            get
            {
                lock (Gate)
                    return outcome;
            }
        }

        /// <summary>Failure, if the outcome is <see cref="OperationOutcome.Failed"/>.</summary>
        public Exception Error
        {
            get
            {
                lock (Gate)
                    return error;
            }
        }

        /// <summary>Optional outcome explanation.</summary>
        public string Detail
        {
            get
            {
                lock (Gate)
                    return detail;
            }
        }

        /// <summary>Recovery actions the failing implementation supports. Game policy selects one.</summary>
        public RecoveryActions Recovery
        {
            get
            {
                lock (Gate)
                    return recovery;
            }
        }

        /// <summary>Whether the failure could have happened after an external commit. Retry is never offered in that case.</summary>
        public bool MayHaveCommitted
        {
            get
            {
                lock (Gate)
                    return mayHaveCommitted;
            }
        }

        /// <summary>Elapsed time, frozen once the operation completes.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                lock (Gate)
                    return Ticks(
                        StartTimestamp,
                        outcome == OperationOutcome.Running ? Stopwatch.GetTimestamp() : EndTimestamp
                    );
            }
        }

        internal OperationRecord(long id, string category, string name, long started)
        {
            Id = id;
            Category = category;
            Name = name;
            StartTimestamp = started;
            stage = "Started";
        }

        internal void SetStage(string value)
        {
            lock (Gate)
                if (outcome == OperationOutcome.Running)
                    stage = value;
        }

        internal void SetWait(string value)
        {
            lock (Gate)
                if (outcome == OperationOutcome.Running)
                {
                    wait = value;
                    WaitStarted = value == null ? 0 : Stopwatch.GetTimestamp();
                }
        }

        internal bool Complete(
            OperationOutcome result,
            Exception failure,
            string message,
            RecoveryActions actions,
            bool committed
        )
        {
            lock (Gate)
            {
                if (outcome != OperationOutcome.Running)
                    return false;
                outcome = result;
                error = failure;
                detail = message;
                mayHaveCommitted = committed;
                recovery = committed ? actions & ~RecoveryActions.Retry : actions;
                wait = null;
                EndTimestamp = Stopwatch.GetTimestamp();
                return true;
            }
        }

        internal static TimeSpan Ticks(long from, long to) =>
            TimeSpan.FromTicks(
                (long)((to - from) * (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency))
            );

        /// <summary>Formats a compact summary on demand.</summary>
        public override string ToString() =>
            $"#{Id} {Category}/{Name} {Outcome} at {Stage} ({Elapsed.TotalMilliseconds:0.###} ms)";
    }

    /// <summary>Capture policy. Shipping builds typically keep bounded history and disable traces.</summary>
    public sealed class DiagnosticsOptions
    {
        /// <summary>When false, scopes are inert and no records are allocated.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Maximum completed operations retained, oldest first discarded. Zero retains none.</summary>
        public int HistoryCapacity { get; set; } = 128;

        /// <summary>Detailed trace capture. Disabled by default.</summary>
        public bool RecordTraces { get; set; }

        /// <summary>Maximum retained trace entries.</summary>
        public int TraceCapacity { get; set; } = 1024;

        /// <summary>Default development policy: bounded history, no detailed traces.</summary>
        public static DiagnosticsOptions Development() => new DiagnosticsOptions();

        /// <summary>Default shipping policy: small bounded history and no traces.</summary>
        public static DiagnosticsOptions Shipping() =>
            new DiagnosticsOptions { HistoryCapacity = 16, RecordTraces = false };

        /// <summary>No operation capture.</summary>
        public static DiagnosticsOptions Disabled() =>
            new DiagnosticsOptions { Enabled = false, HistoryCapacity = 0 };
    }

    /// <summary>Observed operations, bounded completion history and opt-in traces.</summary>
    /// <remarks>Thread-safe. The log never imposes timeouts, retries or recovery; it records what owners report.</remarks>
    public sealed class OperationLog
    {
        private readonly object gate = new object();
        private readonly Dictionary<long, OperationRecord> active = new Dictionary<long, OperationRecord>();
        private readonly Queue<OperationRecord> history = new Queue<OperationRecord>();
        private readonly Queue<TraceEntry> traces = new Queue<TraceEntry>();
        private readonly long created = Stopwatch.GetTimestamp();
        private long next;
        private long started,
            succeeded,
            failed,
            cancelled,
            unsupported,
            unavailable,
            abandoned;

        /// <summary>Current capture policy.</summary>
        public DiagnosticsOptions Options { get; }

        /// <summary>Creates a log with the supplied policy, or the development policy.</summary>
        public OperationLog(DiagnosticsOptions options = null)
        {
            Options = options ?? DiagnosticsOptions.Development();
            if (Options.HistoryCapacity < 0 || Options.TraceCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(options));
        }

        /// <summary>Total operations begun while enabled.</summary>
        public long Started => Interlocked.Read(ref started);

        /// <summary>Counts by outcome.</summary>
        public long Count(OperationOutcome outcome)
        {
            switch (outcome)
            {
                case OperationOutcome.Succeeded:
                    return Interlocked.Read(ref succeeded);
                case OperationOutcome.Failed:
                    return Interlocked.Read(ref failed);
                case OperationOutcome.Cancelled:
                    return Interlocked.Read(ref cancelled);
                case OperationOutcome.Unsupported:
                    return Interlocked.Read(ref unsupported);
                case OperationOutcome.Unavailable:
                    return Interlocked.Read(ref unavailable);
                case OperationOutcome.Abandoned:
                    return Interlocked.Read(ref abandoned);
                default:
                    lock (gate)
                        return active.Count;
            }
        }

        /// <summary>Begins an operation. The returned scope is inert when capture is disabled.</summary>
        public OperationScope Begin(string category, string name)
        {
            if (!Options.Enabled)
                return default;
            if (string.IsNullOrEmpty(category))
                throw new ArgumentException("Category is required.", nameof(category));
            var record = new OperationRecord(
                Interlocked.Increment(ref next),
                category,
                name ?? string.Empty,
                Stopwatch.GetTimestamp()
            );
            Interlocked.Increment(ref started);
            lock (gate)
                active.Add(record.Id, record);
            return new OperationScope(this, record);
        }

        /// <summary>Operations currently running, in start order.</summary>
        public OperationRecord[] GetActive()
        {
            lock (gate)
            {
                var values = new OperationRecord[active.Count];
                active.Values.CopyTo(values, 0);
                Array.Sort(values, (a, b) => a.Id.CompareTo(b.Id));
                return values;
            }
        }

        /// <summary>Completed operations retained by the history policy, oldest first.</summary>
        public OperationRecord[] GetHistory()
        {
            lock (gate)
                return history.ToArray();
        }

        /// <summary>Retained traces, oldest first. Empty unless traces are enabled.</summary>
        public TraceEntry[] GetTraces()
        {
            lock (gate)
                return traces.ToArray();
        }

        /// <summary>Records an unscoped trace without formatting it.</summary>
        public void Trace<T0>(string template, T0 argument) => Trace(0, template, argument, (object)null);

        /// <summary>Discards completed history and traces. Running operations are unaffected.</summary>
        public void ClearHistory()
        {
            lock (gate)
            {
                history.Clear();
                traces.Clear();
            }
        }

        internal void Trace<T0, T1>(long operation, string template, T0 first, T1 second)
        {
            if (!Options.RecordTraces || Options.TraceCapacity == 0)
                return;
            var entry = new TraceEntry(
                operation,
                OperationRecord.Ticks(created, Stopwatch.GetTimestamp()),
                template,
                first,
                second
            );
            lock (gate)
            {
                traces.Enqueue(entry);
                while (traces.Count > Options.TraceCapacity)
                    traces.Dequeue();
            }
        }

        internal void Finish(
            OperationRecord record,
            OperationOutcome outcome,
            Exception error,
            string detail,
            RecoveryActions actions,
            bool committed
        )
        {
            if (!record.Complete(outcome, error, detail, actions, committed))
                return;
            switch (outcome)
            {
                case OperationOutcome.Succeeded:
                    Interlocked.Increment(ref succeeded);
                    break;
                case OperationOutcome.Failed:
                    Interlocked.Increment(ref failed);
                    break;
                case OperationOutcome.Cancelled:
                    Interlocked.Increment(ref cancelled);
                    break;
                case OperationOutcome.Unsupported:
                    Interlocked.Increment(ref unsupported);
                    break;
                case OperationOutcome.Unavailable:
                    Interlocked.Increment(ref unavailable);
                    break;
                default:
                    Interlocked.Increment(ref abandoned);
                    break;
            }
            lock (gate)
            {
                active.Remove(record.Id);
                if (Options.HistoryCapacity == 0)
                    return;
                history.Enqueue(record);
                while (history.Count > Options.HistoryCapacity)
                    history.Dequeue();
            }
        }
    }

    /// <summary>Owner-reported progress for one operation. A default scope is inert.</summary>
    /// <remarks>Complete it exactly once. Disposing a scope that is still running records <see cref="OperationOutcome.Abandoned"/>.</remarks>
    public readonly struct OperationScope : IDisposable
    {
        private readonly OperationLog log;
        private readonly OperationRecord record;

        internal OperationScope(OperationLog log, OperationRecord record)
        {
            this.log = log;
            this.record = record;
        }

        /// <summary>The observed record, or null when capture is disabled.</summary>
        public OperationRecord Record => record;

        /// <summary>Records the current stage.</summary>
        public void Stage(string stage) => record?.SetStage(stage);

        /// <summary>Records a known wait, such as a dependency or backend operation. Pass null when it ends.</summary>
        public void Waiting(string reason) => record?.SetWait(reason);

        /// <summary>Records a trace entry when traces are enabled. Arguments are formatted only on read.</summary>
        public void Trace<T0>(string template, T0 argument)
        {
            if (record != null)
                log.Trace(record.Id, template, argument, (object)null);
        }

        /// <summary>Records a two-argument trace entry when traces are enabled.</summary>
        public void Trace<T0, T1>(string template, T0 first, T1 second)
        {
            if (record != null)
                log.Trace(record.Id, template, first, second);
        }

        /// <summary>Records success.</summary>
        public void Succeed(string detail = null) =>
            Finish(OperationOutcome.Succeeded, null, detail, RecoveryActions.None, false);

        /// <summary>Records cancellation.</summary>
        public void Cancel(string detail = null) =>
            Finish(OperationOutcome.Cancelled, null, detail, RecoveryActions.None, false);

        /// <summary>Records an unsupported capability.</summary>
        public void Unsupported(string detail = null) =>
            Finish(OperationOutcome.Unsupported, null, detail, RecoveryActions.None, false);

        /// <summary>Records temporary unavailability.</summary>
        public void Unavailable(string detail = null, RecoveryActions recovery = RecoveryActions.Retry) =>
            Finish(OperationOutcome.Unavailable, null, detail, recovery, false);

        /// <summary>Records failure with the recovery actions the implementation supports.</summary>
        /// <param name="error">Failure.</param>
        /// <param name="recovery">Supported actions; game policy chooses.</param>
        /// <param name="mayHaveCommitted">True when an external commit may already have happened. Retry is then removed.</param>
        /// <param name="detail">Optional explanation.</param>
        public void Fail(
            Exception error,
            RecoveryActions recovery = RecoveryActions.None,
            bool mayHaveCommitted = false,
            string detail = null
        ) => Finish(OperationOutcome.Failed, error, detail ?? error?.Message, recovery, mayHaveCommitted);

        /// <summary>Records abandonment if no outcome was recorded.</summary>
        public void Dispose() =>
            Finish(OperationOutcome.Abandoned, null, null, RecoveryActions.None, false);

        private void Finish(
            OperationOutcome outcome,
            Exception error,
            string detail,
            RecoveryActions recovery,
            bool committed
        )
        {
            if (record != null)
                log.Finish(record, outcome, error, detail, recovery, committed);
        }
    }
}
