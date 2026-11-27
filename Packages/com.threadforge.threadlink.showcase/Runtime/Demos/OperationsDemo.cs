namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Operation records in the session log and the Runtime monitor.</summary>
    public sealed class OperationsDemo : ShowcaseDemo
    {
        public override string Title => "Operations";

        public override string Summary =>
            "Threadlink.Begin records an operation with stages and an explicit outcome: succeeded, cancelled, failed, unsupported or unavailable. Every subsystem reports itself too. Open Threadlink ▸ Runtime Monitor during Play mode to see both, live.";

        public override string[] Packages => new[] { "core" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var counts = Value();
            void Show() => counts.text = $"Started {Threadlink.Operations.Started}, failed {Threadlink.Operations.Count(OperationOutcome.Failed)}";
            Show();
            panel.Add(counts);
            panel.Add(Row(
                Action(context, "Record a save", async () =>
                {
                    var save = Threadlink.BeginOperation("Saves", "Manual save");
                    save.Stage("Writing slot 0");
                    await UniTask.WaitForSeconds(0.3f);
                    save.Succeed();
                    Show();
                }),
                Action(context, "Record a failure", () =>
                {
                    var upload = Threadlink.BeginOperation("Cloud", "Upload");
                    upload.Fail(new InvalidOperationException("Offline"));
                    Show();
                })));
        }

        public override UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            long started = Threadlink.Operations.Started;
            var operation = Threadlink.BeginOperation("Showcase", "Smoke");
            operation.Stage("Checking");
            Check(operation.Record.Stage == "Checking", "stages are recorded");
            operation.Succeed();
            Check(operation.Record.Outcome == OperationOutcome.Succeeded, "the outcome is recorded");
            Check(Threadlink.Operations.Started == started + 1, "the session log counts operations");
            var report = new MonitorReport();
            report.Begin(true);
            Threadlink.Instance.Report(report);
            Check(report.Lines.Count > 0, "the core reports itself for the Runtime monitor");
            return UniTask.CompletedTask;
        }
    }
}
