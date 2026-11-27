namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.PackageManager;
    using UnityEditor.PackageManager.Requests;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.UIElements;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;
    using Sample = UnityEditor.PackageManager.UI.Sample;

    /// <summary>
    /// Every Threadlink package in one place: what each does, what is installed, and one-click install, update, removal,
    /// sample import and the Showcase.
    /// </summary>
    public sealed class HubPage : WorkspacePage
    {
        /// <summary>The Showcase package.</summary>
        public const string ShowcasePackage = "com.threadforge.threadlink.showcase";

        private static Request pending;
        private static string pendingLabel;
        private static Action finished;
        private WorkspaceContext context;
        private VisualElement list;
        private Label status;
        private string filter = string.Empty;

        /// <inheritdoc />
        public override string Title => "Hub";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 0;

        /// <summary>Whether a Package Manager request started from the Hub is running.</summary>
        public static bool Busy => pending != null;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            this.context = context;
            var root = new VisualElement();
            HubCatalog catalog;
            try
            {
                catalog = HubCatalog.Load();
            }
            catch (Exception error)
            {
                root.Add(WorkspaceUI.Row("Catalog", error.Message, severity: IssueSeverity.Error));
                return root;
            }
            var installed = Installed();
            root.Add(
                WorkspaceUI.Text(
                    $"Threadlink {catalog.version}: {catalog.packages.Length} packages. Installing a package never starts services or changes scenes; each package documents how to compose it."
                )
            );
            status = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } };
            root.Add(status);
            AddRegistry(root, catalog);
            AddFramework(root, catalog, installed);
            var search = new TextField("Filter") { value = filter, tooltip = "Show packages whose name or description contains this text." };
            search.RegisterValueChangedCallback(change =>
            {
                filter = change.newValue ?? string.Empty;
                Fill(catalog, Installed());
            });
            root.Add(search);
            list = new VisualElement();
            root.Add(list);
            Fill(catalog, installed);
            ShowStatus();
            return root;
        }

        /// <summary>Rebuilds the page after a Package Manager change.</summary>
        private void Reload() => context?.Refresh();

        private void AddRegistry(VisualElement root, HubCatalog catalog)
        {
            bool configured;
            try
            {
                configured = ProjectManifest.HasRegistry(File.ReadAllText(ProjectManifest.Path), catalog.registry, catalog.scopes);
            }
            catch (Exception error)
            {
                root.Add(WorkspaceUI.Row("Registry", "Packages/manifest.json could not be read: " + error.Message, severity: IssueSeverity.Error));
                return;
            }
            if (configured)
            {
                root.Add(WorkspaceUI.Row("Registry", $"{catalog.registry} serves {string.Join(", ", catalog.scopes)}."));
                return;
            }
            root.Add(
                WorkspaceUI.Row(
                    "Registry",
                    $"Threadlink packages install from OpenUPM. Add {catalog.registry} with the scopes {string.Join(", ", catalog.scopes)} to Packages/manifest.json.",
                    "Add Registry",
                    () =>
                    {
                        if (!EditorUtility.DisplayDialog("Add OpenUPM registry", $"Add the scoped registry {catalog.registry} ({string.Join(", ", catalog.scopes)}) to Packages/manifest.json?", "Add", "Cancel"))
                            return;
                        ProjectManifest.EnsureRegistry("OpenUPM", catalog.registry, catalog.scopes);
                        Client.Resolve();
                        Reload();
                    },
                    IssueSeverity.Warning
                )
            );
        }

        private void AddFramework(VisualElement root, HubCatalog catalog, Dictionary<string, PackageInfo> installed)
        {
            var buttons = new List<(string, Action)>();
            var missing = catalog
                .packages.Where(p => !catalog.framework_excludes.Contains(p.name) && !installed.ContainsKey(p.name))
                .Select(p => p.name + "@" + catalog.version)
                .ToArray();
            if (missing.Length > 0)
                buttons.Add(("Install Complete Framework", () => Run(() => Client.AddAndRemove(missing), "Installing " + missing.Length + " packages")));
            if (installed.ContainsKey(ShowcasePackage))
                buttons.Add(("Open Showcase", OpenShowcase));
            else if (catalog.packages.Any(p => p.name == ShowcasePackage))
                buttons.Add(("Install Showcase", () => Add(ShowcasePackage + "@" + catalog.version)));
            buttons.Add(("Package Manager", () => UnityEditor.PackageManager.UI.Window.Open(string.Empty)));
            buttons.Add(("Validation", () => context.OpenPage<ValidationPage>()));
            root.Add(WorkspaceUI.Buttons(buttons.ToArray()));
        }

        private void Fill(HubCatalog catalog, Dictionary<string, PackageInfo> installed)
        {
            list.Clear();
            foreach (var family in catalog.packages.Where(Matches).GroupBy(p => p.family))
            {
                list.Add(WorkspaceUI.Heading(family.Key));
                foreach (var entry in family)
                    list.Add(Card(catalog, entry, installed));
            }
        }

        private bool Matches(HubCatalog.Entry entry) =>
            filter.Length == 0
            || entry.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || entry.displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || entry.description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private VisualElement Card(HubCatalog catalog, HubCatalog.Entry entry, Dictionary<string, PackageInfo> installed)
        {
            var card = new VisualElement();
            card.style.marginBottom = 6;
            card.style.paddingLeft = 6;
            card.style.borderLeftWidth = 2;
            card.style.borderLeftColor = installed.ContainsKey(entry.name) ? (Color)new Color32(90, 158, 255, 255) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
            installed.TryGetValue(entry.name, out var info);
            string state = State(catalog, entry, info, installed);
            card.Add(WorkspaceUI.Row(entry.displayName, state, severity: info != null && info.version != catalog.version ? IssueSeverity.Warning : IssueSeverity.Info));
            card.Add(WorkspaceUI.Text(entry.description));
            var buttons = new List<(string, Action)>();
            if (info == null)
                buttons.Add(("Install", () => Add(entry.name + "@" + catalog.version)));
            else if (Managed(info) && info.version != catalog.version)
                buttons.Add(($"Update to {catalog.version}", () => Add(entry.name + "@" + catalog.version)));
            if (info != null && Managed(info) && info.isDirectDependency && !RequiredBy(entry.name, installed, catalog).Any())
                buttons.Add(("Remove", () => Remove(entry.name)));
            if (info != null)
                foreach (var sample in Sample.FindByPackage(entry.name, info.version))
                {
                    var current = sample;
                    buttons.Add((current.isImported ? $"Reimport “{current.displayName}”" : $"Import “{current.displayName}”", () => Import(current)));
                }
            if (!string.IsNullOrEmpty(entry.documentationUrl))
                buttons.Add(("Docs", () => Application.OpenURL(entry.documentationUrl)));
            card.Add(WorkspaceUI.Buttons(buttons.ToArray()));
            return card;
        }

        private static string State(HubCatalog catalog, HubCatalog.Entry entry, PackageInfo info, Dictionary<string, PackageInfo> installed)
        {
            if (info == null)
                return "Not installed" + (entry.dependencies.Length > 0 ? "; installs " + string.Join(", ", entry.dependencies.Select(Short)) : string.Empty);
            string text = $"Installed {info.version} ({info.source})";
            var requiredBy = RequiredBy(entry.name, installed, catalog).ToList();
            if (!info.isDirectDependency && requiredBy.Count > 0)
                text += "; required by " + string.Join(", ", requiredBy.Select(Short));
            if (info.version != catalog.version)
                text += $"; this release is {catalog.version}";
            return text;
        }

        private static IEnumerable<string> RequiredBy(string name, Dictionary<string, PackageInfo> installed, HubCatalog catalog) =>
            catalog.packages.Where(p => installed.ContainsKey(p.name) && p.dependencies.Contains(name)).Select(p => p.name);

        private static string Short(string name) => name.Replace("com.threadforge.threadlink.", string.Empty);

        /// <summary>Embedded, local and built-in packages are managed by the project, not by the Hub.</summary>
        private static bool Managed(PackageInfo info) =>
            info.source == PackageSource.Registry || info.source == PackageSource.LocalTarball || info.source == PackageSource.Git;

        /// <summary>Installed Threadlink packages by name.</summary>
        public static Dictionary<string, PackageInfo> Installed() =>
            PackageInfo.GetAllRegisteredPackages()
                .Where(p => p.name.StartsWith("com.threadforge.", StringComparison.Ordinal))
                .GroupBy(p => p.name)
                .ToDictionary(g => g.Key, g => g.First());

        private void Add(string identifier) => Run(() => Client.Add(identifier), "Installing " + identifier);

        private void Remove(string name)
        {
            if (EditorUtility.DisplayDialog("Remove package", $"Remove {name} from this project?", "Remove", "Cancel"))
                Run(() => Client.Remove(name), "Removing " + name);
        }

        private void Import(Sample sample)
        {
            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
                return;
            AssetDatabase.Refresh();
            Reload();
        }

        /// <summary>Imports the Showcase sample if needed and opens its gallery scene.</summary>
        public static void OpenShowcase()
        {
            var info = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == ShowcasePackage);
            var sample = info == null ? default : Sample.FindByPackage(ShowcasePackage, info.version).FirstOrDefault();
            if (info == null || string.IsNullOrEmpty(sample.displayName))
            {
                Debug.LogWarning("Install the Threadlink Showcase package first.");
                return;
            }
            if (!sample.isImported && !sample.Import(Sample.ImportOptions.HideImportWindow))
                return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string scene = Directory.EnumerateFiles(sample.importPath, "Showcase.unity", SearchOption.AllDirectories).FirstOrDefault();
            if (scene == null)
            {
                Debug.LogWarning("The Showcase sample has no Showcase scene.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ToAssetPath(scene));
        }

        private static string ToAssetPath(string fullPath)
        {
            string project = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return Path.GetFullPath(fullPath).Substring(project.Length).Replace('\\', '/');
        }

        private void Run(Func<Request> start, string label)
        {
            if (Busy)
                return;
            pendingLabel = label;
            pending = start();
            finished = Reload;
            EditorApplication.update += Poll;
            ShowStatus();
        }

        private void ShowStatus()
        {
            if (status != null)
                status.text = Busy ? pendingLabel + "…" : string.Empty;
        }

        private static void Poll()
        {
            if (pending == null || !pending.IsCompleted)
                return;
            EditorApplication.update -= Poll;
            if (pending.Status == StatusCode.Failure)
                Debug.LogError($"{pendingLabel} failed: {pending.Error?.message}");
            pending = null;
            var callback = finished;
            finished = null;
            callback?.Invoke();
        }
    }
}
