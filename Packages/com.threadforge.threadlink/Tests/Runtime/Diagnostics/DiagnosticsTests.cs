namespace Threadforge.Threadlink.Tests
{
    using System;
    using NUnit.Framework;

    public sealed class DiagnosticsTests
    {
        [Test]
        public void OutcomesRemainDistinctAndFirstOutcomeWins()
        {
            var log = new OperationLog();
            var unsupported = log.Begin("Platform", "Achievements");
            var unavailable = log.Begin("Platform", "Leaderboard");
            var cancelled = log.Begin("Content", "Portrait");
            var failed = log.Begin("Persistence", "Save");
            var succeeded = log.Begin("Services", "Startup");
            unsupported.Unsupported();
            unavailable.Unavailable("offline");
            cancelled.Cancel();
            failed.Fail(new InvalidOperationException("disk"));
            succeeded.Succeed();
            succeeded.Fail(new Exception("late"));
            succeeded.Dispose();

            Assert.That(unsupported.Record.Outcome, Is.EqualTo(OperationOutcome.Unsupported));
            Assert.That(unavailable.Record.Outcome, Is.EqualTo(OperationOutcome.Unavailable));
            Assert.That(cancelled.Record.Outcome, Is.EqualTo(OperationOutcome.Cancelled));
            Assert.That(failed.Record.Outcome, Is.EqualTo(OperationOutcome.Failed));
            Assert.That(failed.Record.Error.Message, Is.EqualTo("disk"));
            Assert.That(succeeded.Record.Outcome, Is.EqualTo(OperationOutcome.Succeeded));
            Assert.That(succeeded.Record.Error, Is.Null);
            foreach (
                var outcome in new[]
                {
                    OperationOutcome.Succeeded,
                    OperationOutcome.Cancelled,
                    OperationOutcome.Failed,
                    OperationOutcome.Unsupported,
                    OperationOutcome.Unavailable,
                }
            )
                Assert.That(log.Count(outcome), Is.EqualTo(1), outcome.ToString());
            Assert.That(log.Count(OperationOutcome.Running), Is.Zero);
            Assert.That(log.GetActive(), Is.Empty);
        }

        [Test]
        public void StagesWaitsAndElapsedTimeAreRecordedUntilCompletion()
        {
            var log = new OperationLog();
            using var scope = log.Begin("Nexus", "Forest");
            scope.Stage("Loading");
            scope.Waiting("Services ready");
            Assert.That(log.GetActive(), Has.Length.EqualTo(1));
            Assert.That(scope.Record.Waiting, Is.EqualTo("Services ready"));
            Assert.That(scope.Record.Stage, Is.EqualTo("Loading"));
            scope.Waiting(null);
            scope.Stage("Presenting");
            System.Threading.Thread.Sleep(2);
            scope.Succeed();
            var elapsed = scope.Record.Elapsed;
            Assert.That(elapsed, Is.GreaterThan(TimeSpan.Zero));
            scope.Stage("Ignored after completion");
            Assert.That(scope.Record.Stage, Is.EqualTo("Presenting"));
            Assert.That(scope.Record.Elapsed, Is.EqualTo(elapsed));
        }

        [Test]
        public void PotentiallyCommittedFailuresNeverOfferRetry()
        {
            var log = new OperationLog();
            var safe = log.Begin("Persistence", "Prepare");
            safe.Fail(new Exception("before commit"), RecoveryActions.Retry | RecoveryActions.Reconcile);
            var committed = log.Begin("Persistence", "Publish");
            committed.Fail(
                new Exception("after commit"),
                RecoveryActions.Retry | RecoveryActions.Reconcile,
                mayHaveCommitted: true
            );
            Assert.That(safe.Record.Recovery, Is.EqualTo(RecoveryActions.Retry | RecoveryActions.Reconcile));
            Assert.That(committed.Record.Recovery, Is.EqualTo(RecoveryActions.Reconcile));
            Assert.That(committed.Record.MayHaveCommitted, Is.True);
        }

        [Test]
        public void HistoryAndTracesAreBoundedAndDisposalRecordsAbandonment()
        {
            var log = new OperationLog(
                new DiagnosticsOptions { HistoryCapacity = 3, Traces = true, TraceCapacity = 2 }
            );
            for (int i = 0; i < 5; i++)
            {
                var scope = log.Begin("Test", "Op" + i);
                scope.Trace("step {0}", i);
                scope.Dispose();
            }
            var history = log.GetHistory();
            Assert.That(history, Has.Length.EqualTo(3));
            Assert.That(history[0].Name, Is.EqualTo("Op2"));
            Assert.That(history[2].Outcome, Is.EqualTo(OperationOutcome.Abandoned));
            var traces = log.GetTraces();
            Assert.That(traces, Has.Length.EqualTo(2));
            Assert.That(traces[1].ToString(), Is.EqualTo("step 4"));
            Assert.That(log.Count(OperationOutcome.Abandoned), Is.EqualTo(5));
        }

        [Test]
        public void TracesAreOptIn()
        {
            var log = new OperationLog();
            var scope = log.Begin("Test", "Op");
            scope.Trace("value {0}", 1);
            scope.Succeed();
            Assert.That(log.GetTraces(), Is.Empty);
        }

        [Test]
        public void DisabledCaptureAllocatesNothing()
        {
            var log = new OperationLog(DiagnosticsOptions.Disabled());
            for (int i = 0; i < 100; i++)
                Exercise(log, i);
            using var measurement = AllocationMeasurement.Start();
            for (int i = 0; i < 10000; i++)
                Exercise(log, i);
            measurement.AssertNoAllocations();
            Assert.That(log.Started, Is.Zero);
            Assert.That(log.GetHistory(), Is.Empty);
        }

        private static void Exercise(OperationLog log, int value)
        {
            var scope = log.Begin("Test", "Disabled");
            scope.Stage("Stage");
            scope.Trace("value {0}", value);
            scope.Succeed();
        }
    }
}
