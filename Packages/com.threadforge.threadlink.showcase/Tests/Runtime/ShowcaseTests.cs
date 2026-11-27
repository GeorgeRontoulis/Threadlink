namespace Threadforge.Threadlink.Showcase.Tests
{
    using Cysharp.Threading.Tasks;
    using System.Collections;
    using System.Linq;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class ShowcaseTests
    {
        private GameObject host;

        [SetUp]
        public void CreateHost()
        {
            host = new GameObject("Showcase test host");
            host.AddComponent<AudioListener>();
            // Screens open UI Toolkit documents on the panel of a document already on screen.
            var document = host.AddComponent<UnityEngine.UIElements.UIDocument>();
            document.panelSettings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
        }

        [TearDown]
        public void DestroyHost() => Object.Destroy(host);

        [UnityTest]
        public IEnumerator EveryDemoPassesItsSmokeCheck()
        {
            using var context = new ShowcaseContext(host);
            var run = ShowcaseApp.SmokeAllAsync(context, CancellationToken.None);
            float end = Time.realtimeSinceStartup + 120;
            while (!run.Status.IsCompleted() && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.That(run.Status.IsCompleted(), "Showcase smoke checks timed out.");
            string summary;
            try
            {
                summary = run.GetAwaiter().GetResult();
            }
            catch (System.Exception error)
            {
                Assert.Fail(error.GetBaseException().ToString());
                throw;
            }
            StringAssert.StartsWith("PASS", summary);
        }

        [Test]
        public void DemosHaveTitlesSummariesAndPackages()
        {
            var titles = ShowcaseApp.Demos.Select(create =>
            {
                using var demo = create();
                Assert.That(demo.Summary, Is.Not.Empty, demo.Title);
                Assert.That(demo.Packages, Is.Not.Empty, demo.Title);
                return demo.Title;
            }).ToList();
            Assert.That(titles, Is.Unique);
            Assert.That(titles.Count, Is.EqualTo(15));
        }
    }
}
