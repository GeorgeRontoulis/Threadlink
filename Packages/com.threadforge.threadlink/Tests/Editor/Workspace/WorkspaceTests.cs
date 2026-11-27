namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine.UIElements;

    public sealed class WorkspaceTests
    {
        private sealed class Reporting : IWorkspaceValidator
        {
            public string Name => "Reporting";

            public void Validate(List<ValidationIssue> issues) =>
                issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, "warned"));
        }

        private sealed class Throwing : IWorkspaceValidator
        {
            public string Name => "Throwing";

            public void Validate(List<ValidationIssue> issues)
            {
                issues.Add(new ValidationIssue(Name, IssueSeverity.Info, "partial"));
                throw new InvalidOperationException("broken");
            }
        }

        [Test]
        public void FailingValidatorIsReportedWithoutPartialResultsOrStoppingOthers()
        {
            var issues = WorkspaceValidation.Run(new IWorkspaceValidator[] { new Throwing(), new Reporting() });
            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues[0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(issues[0].Message, Does.Contain("broken"));
            Assert.That(issues[1].Message, Is.EqualTo("warned"));
        }

        [Test]
        public void DiscoveryIgnoresNonPublicValidatorTypes()
        {
            var names = WorkspaceValidation.Discover().Select(validator => validator.Name).ToList();
            Assert.That(names, Has.No.Member("Reporting"));
            Assert.That(names, Has.No.Member("Throwing"));
        }

        [Test]
        public void HubCatalogListsEveryInstalledThreadlinkPackageAtOneVersion()
        {
            var catalog = HubCatalog.Load();
            Assert.That(catalog.version, Is.Not.Empty);
            Assert.That(catalog.packages.Select(p => p.name), Does.Contain(catalog.framework));
            var names = catalog.packages.Select(p => p.name).ToList();
            foreach (var installed in HubPage.Installed().Keys.Where(n => n.StartsWith("com.threadforge.threadlink", StringComparison.Ordinal)))
                Assert.That(names, Does.Contain(installed), installed);
            foreach (var entry in catalog.packages)
            {
                Assert.That(entry.description, Is.Not.Empty, entry.name);
                Assert.That(entry.documentationUrl, Does.StartWith("https://"), entry.name);
                foreach (var dependency in entry.dependencies)
                    Assert.That(names, Does.Contain(dependency), entry.name + " -> " + dependency);
            }
        }

        [Test]
        public void ManifestRegistryEditKeepsOtherEntriesAndIsIdempotent()
        {
            const string original =
                "{\n  \"dependencies\": {\n    \"com.unity.ugui\": \"2.6.0\",\n    \"com.example.tool\": \"file:../Tool\"\n  },\n  \"testables\": [\"com.example.tool\"]\n}\n";
            var scopes = new[] { "com.threadforge", "com.cysharp" };
            Assert.That(ProjectManifest.HasRegistry(original, "https://package.openupm.com", scopes), Is.False);
            string added = ProjectManifest.AddRegistry(original, "OpenUPM", "https://package.openupm.com", scopes);
            Assert.That(ProjectManifest.HasRegistry(added, "https://package.openupm.com/", scopes), Is.True);
            StringAssert.Contains("\"com.example.tool\": \"file:../Tool\"", added);
            StringAssert.Contains("\"testables\": [\n    \"com.example.tool\"\n  ]", added);
            Assert.That(ProjectManifest.AddRegistry(added, "OpenUPM", "https://package.openupm.com", scopes), Is.EqualTo(added));
            string partial = ProjectManifest.AddRegistry(original, "OpenUPM", "https://package.openupm.com", new[] { "com.cysharp" });
            Assert.That(ProjectManifest.HasRegistry(partial, "https://package.openupm.com", scopes), Is.False);
            Assert.That(ProjectManifest.HasRegistry(ProjectManifest.AddRegistry(partial, "x", "https://package.openupm.com", scopes), "https://package.openupm.com", scopes), Is.True);
        }

        [Test]
        public void BuiltInPagesAreDiscoveredInGroupOrder()
        {
            var pages = WorkspaceWindow.DiscoverPages();
            Assert.That(pages.OfType<HubPage>().Count(), Is.EqualTo(1));
            Assert.That(pages.OfType<ValidationPage>().Count(), Is.EqualTo(1));
            Assert.That(pages[0], Is.TypeOf<HubPage>());
            var groups = pages.Select(page => page.Group).ToList();
            Assert.That(groups.IndexOf("Project"), Is.LessThan(groups.IndexOf("Modules")));
        }

        [Test]
        public void EveryInstalledPageBuildsInTheWindow()
        {
            var window = WorkspaceWindow.Open();
            try
            {
                Assert.That(window.Pages, Is.Not.Empty);
                foreach (var page in window.Pages.ToList())
                {
                    Assert.That(window.Show(page.GetType()), Is.True, page.Title);
                    Assert.That(window.CurrentPage, Is.SameAs(page));
                    Assert.That(
                        window.rootVisualElement.Query<HelpBox>().ToList()
                            .Any(box => box.text.Contains("failed to build")),
                        Is.False,
                        page.Title
                    );
                }
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void DistributionFilterIncludesThirdPartyDependencies()
        {
            Assert.That(Compatibility.IsDistributed("com.threadforge.threadlink.iris"), Is.True);
            Assert.That(Compatibility.IsDistributed("com.cysharp.unitask"), Is.True);
            Assert.That(Compatibility.IsDistributed("com.unity.addressables"), Is.False);
        }
    }
}
