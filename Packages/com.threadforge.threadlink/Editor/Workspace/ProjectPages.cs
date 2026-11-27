namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;

    /// <summary>Compares installed packages with the starter's tested compatibility manifest, if installed.</summary>
    public static class Compatibility
    {
        /// <summary>Starter package name. The workspace reads its manifest file without referencing its assemblies.</summary>
        public const string StarterPackage = "com.threadforge.threadlink.starter";

        [Serializable]
        private sealed class Manifest
        {
            public string set;
            public Entry[] packages;
        }

        [Serializable]
        private sealed class Entry
        {
            public string name;
            public string version;
        }

        /// <summary>Comparison outcome.</summary>
        public sealed class Report
        {
            /// <summary>Human-readable summary.</summary>
            public string Summary;

            /// <summary>Packages whose installed version differs from the tested version.</summary>
            public List<(string package, string message)> Issues = new List<(string, string)>();
        }

        /// <summary>Whether a package belongs to the Threadlink distribution.</summary>
        public static bool IsDistributed(string name) =>
            name.StartsWith("com.threadforge.", StringComparison.Ordinal) || name == "com.cysharp.unitask";

        /// <summary>Returns null when the starter is not installed.</summary>
        public static Report Check(IReadOnlyList<PackageInfo> installed)
        {
            var starter = installed.FirstOrDefault(package => package.name == StarterPackage);
            if (starter == null)
                return null;
            string path = Path.Combine(starter.resolvedPath, "compatibility.json");
            var report = new Report();
            if (!File.Exists(path))
            {
                report.Summary = "The installed starter has no compatibility.json.";
                return report;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            int matched = 0;
            foreach (var entry in manifest?.packages ?? Array.Empty<Entry>())
            {
                var package = installed.FirstOrDefault(candidate => candidate.name == entry.name);
                if (package == null)
                    continue;
                if (package.version == entry.version)
                    matched++;
                else
                    report.Issues.Add((entry.name, $"Installed {package.version}; tested {entry.version}."));
            }
            report.Summary =
                $"Tested set {manifest?.set ?? "(unnamed)"}: {matched} installed package(s) match, {report.Issues.Count} differ. Packages outside the set are not covered by the starter's tests.";
            return report;
        }
    }

    /// <summary>Runs every installed module validator.</summary>
    public sealed class ValidationPage : WorkspacePage
    {
        private VisualElement results;

        /// <inheritdoc />
        public override string Title => "Validation";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 10;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            var validators = WorkspaceValidation.Discover();
            root.Add(
                WorkspaceUI.Text(
                    $"{validators.Count} validator(s): {string.Join(", ", validators.Select(v => v.Name))}. Validators read the project and use the same checks as the runtime operations; they never modify assets."
                )
            );
            results = new VisualElement();
            root.Add(WorkspaceUI.Buttons(("Run", () => Show(context, validators))));
            root.Add(results);
            Show(context, validators);
            return root;
        }

        private void Show(WorkspaceContext context, IReadOnlyList<IWorkspaceValidator> validators)
        {
            results.Clear();
            var issues = WorkspaceValidation.Run(validators);
            int errors = issues.Count(issue => issue.Severity == IssueSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == IssueSeverity.Warning);
            results.Add(WorkspaceUI.Heading($"{errors} error(s), {warnings} warning(s), {issues.Count - errors - warnings} note(s)"));
            foreach (var issue in issues.OrderByDescending(issue => issue.Severity))
            {
                var target = issue.Context;
                results.Add(
                    WorkspaceUI.Row(
                        issue.Source,
                        issue.Message,
                        target == null ? null : "Select",
                        target == null ? null : () => context.Select(target),
                        issue.Severity
                    )
                );
            }
        }
    }
}
