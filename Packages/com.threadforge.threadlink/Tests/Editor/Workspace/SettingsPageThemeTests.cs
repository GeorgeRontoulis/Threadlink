namespace Threadforge.Threadlink.Editor.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine.UIElements;

    public sealed class SettingsPageThemeTests
    {
        private static IEnumerable<SettingsProvider> Providers()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            yield return (SettingsProvider)typeof(ThreadlinkOverviewProvider).GetMethod("Create", flags).Invoke(null, null);
            foreach (var provider in (SettingsProvider[])typeof(ThreadlinkSettingsProvider).GetMethod("Create", flags).Invoke(null, null))
                yield return provider;
        }

        [Test]
        public void PagesLeaveTheSharedSettingsRootUnstyled()
        {
            // The Settings window gives every page the same root and only clears its children between pages.
            var root = new VisualElement();
            var providers = Providers().ToList();
            Assert.That(providers.Count, Is.EqualTo(ThreadlinkSettingsAssets.Types.Count + 1));
            foreach (var provider in providers)
            {
                provider.OnActivate(string.Empty, root);
                Assert.That(root.childCount, Is.GreaterThan(0), provider.settingsPath);
                provider.OnDeactivate();
                root.Clear();
                Assert.That(root.GetClasses(), Is.Empty, provider.settingsPath);
                Assert.That(root.styleSheets.count, Is.Zero, provider.settingsPath);
            }
        }
    }
}
