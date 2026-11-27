namespace Threadforge.Threadlink.Editor.Tests
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;

    public class ScenesToolbarTests
    {
        [Test]
        public void BuildProfileScenesComeFirstInOrderAndEveryEntryIsListedOnce()
        {
            var entries = ScenesToolbar.Collect();
            var build = EditorBuildSettings.scenes.Where(scene => scene.enabled && File.Exists(scene.path)).Select(scene => scene.path).Distinct().ToList();
            Assert.That(entries.Take(build.Count).Select(entry => entry.Path), Is.EqualTo(build));
            Assert.That(entries.Take(build.Count).Any(entry => entry.IsAddressable), Is.False);
            Assert.That(entries.Skip(build.Count).All(entry => entry.IsAddressable), Is.True);
            Assert.That(entries.Select(entry => entry.Path), Is.Unique);
            Assert.That(entries.All(entry => entry.Path.EndsWith(".unity")), Is.True);
        }
    }
}
