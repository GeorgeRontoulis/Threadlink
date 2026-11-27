namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>Finds, creates and registers the project's Threadlink engineering settings assets.</summary>
    public static class ThreadlinkSettingsAssets
    {
        /// <summary>Folder that holds engineering settings assets.</summary>
        public const string Folder = "Assets/Threadforge/Threadlink User/Engineering/Configs";

        /// <summary>Every concrete settings type in the project.</summary>
        public static IReadOnlyList<Type> Types =>
            TypeCache
                .GetTypesDerivedFrom<ThreadlinkSettings>()
                .Where(type => !type.IsAbstract && !type.IsGenericType)
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>The registered settings asset of <paramref name="type"/>, optionally creating it.</summary>
        public static ThreadlinkSettings Get(Type type, bool create)
        {
            string key = Key(type);
            if (EditorBuildSettings.TryGetConfigObject(key, out ThreadlinkSettings existing) && existing != null)
                return existing;
            string path = Folder + "/" + type.Name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThreadlinkSettings>(path);
            if (asset == null)
            {
                if (!create)
                    return null;
                Directory.CreateDirectory(Folder);
                asset = (ThreadlinkSettings)ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(key, asset, true);
            return asset;
        }

        internal static string Key(Type type) => "com.threadforge.threadlink.settings." + type.Name;

        /// <summary>Registers settings assets as preloaded so players find them before the first scene.</summary>
        internal static void Preload()
        {
            var preloaded = PlayerSettings.GetPreloadedAssets().Where(asset => asset != null && !(asset is ThreadlinkSettings)).ToList();
            foreach (var type in Types)
            {
                var settings = Get(type, false);
                if (settings != null)
                    preloaded.Add(settings);
            }
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        }
    }

    /// <summary>One Project Settings page per settings type, under Threadlink.</summary>
    internal sealed class ThreadlinkSettingsProvider : SettingsProvider
    {
        private readonly Type type;

        private ThreadlinkSettingsProvider(Type type, string page)
            : base("Project/Threadlink/" + page, SettingsScope.Project) => this.type = type;

        [SettingsProviderGroup]
        private static SettingsProvider[] Create() =>
            ThreadlinkSettingsAssets
                .Types.Select(type =>
                {
                    var probe = (ThreadlinkSettings)ScriptableObject.CreateInstance(type);
                    string page = probe.PageName;
                    Object.DestroyImmediate(probe);
                    return (SettingsProvider)new ThreadlinkSettingsProvider(type, page);
                })
                .ToArray();

        public override void OnActivate(string searchContext, VisualElement root)
        {
            root.AddToClassList("threadlink");
            ThreadlinkTheme.Apply(root);
            var page = new VisualElement();
            page.AddToClassList("tl-settings");
            root.Add(page);
            var title = new Label(ObjectNames.NicifyVariableName(type.Name));
            title.AddToClassList("tl-title");
            page.Add(title);
            var note = new Label("Engineering settings. They live in " + ThreadlinkSettingsAssets.Folder + " and ship with players; designers never need to edit them.");
            note.AddToClassList("tl-note");
            page.Add(note);
            var settings = ThreadlinkSettingsAssets.Get(type, false);
            if (settings == null)
            {
                page.Add(new Button(() =>
                {
                    ThreadlinkSettingsAssets.Get(type, true);
                    ThreadlinkSettingsAssets.Preload();
                    root.Clear();
                    OnActivate(searchContext, root);
                }) { text = "Create settings" });
                return;
            }
            var serialized = new SerializedObject(settings);
            var fields = new VisualElement();
            var property = serialized.GetIterator();
            string generatedBy = null;
            if (property.NextVisible(true))
                do
                {
                    if (property.propertyPath == "m_Script")
                        continue;
                    var field = new PropertyField(property.Copy());
                    // Fields Threadlink's code generation writes are shown, never edited, here.
                    var generated = type.GetField(property.name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?.GetCustomAttributes(typeof(GeneratedSettingAttribute), true)
                        .OfType<GeneratedSettingAttribute>()
                        .FirstOrDefault();
                    if (generated != null)
                    {
                        field.SetEnabled(false);
                        generatedBy ??= generated.EditedWith ?? string.Empty;
                    }
                    fields.Add(field);
                } while (property.NextVisible(false));
            if (generatedBy != null)
            {
                var generatedNote = new Label("Generated and read-only. Change it with the tool that writes it.");
                generatedNote.AddToClassList("tl-note");
                page.Add(generatedNote);
                if (generatedBy.Length > 0)
                    page.Add(new Button(() => EditorApplication.ExecuteMenuItem(generatedBy)) { text = "Open " + generatedBy.Substring(generatedBy.LastIndexOf('/') + 1) });
            }
            fields.Bind(serialized);
            page.Add(fields);
            var problem = new Label();
            problem.AddToClassList("tl-problem");
            page.Add(problem);
            void Check()
            {
                string message = settings.Validate();
                problem.text = message ?? string.Empty;
                problem.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            Check();
            fields.TrackSerializedObjectValue(serialized, _ =>
            {
                Check();
                // Settings such as audio categories and achievements are sources of generated IDs.
                AssetDatabase.SaveAssetIfDirty(settings);
                GeneratedIds.Request();
            });
        }
    }

    /// <summary>
    /// The Project Settings ▸ Threadlink page: a read-only overview. The core has no settings of its own; each package's
    /// engineering settings have a page below it.
    /// </summary>
    internal sealed class ThreadlinkOverviewProvider : SettingsProvider
    {
        private ThreadlinkOverviewProvider()
            : base("Project/Threadlink", SettingsScope.Project, new[] { "Threadlink", "subsystems", "engineering settings" }) { }

        [SettingsProvider]
        private static SettingsProvider Create() => new ThreadlinkOverviewProvider();

        public override void OnActivate(string searchContext, VisualElement root)
        {
            ThreadlinkTheme.Apply(root);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            var page = new VisualElement();
            page.AddToClassList("tl-settings");
            page.AddToClassList("tl-overview");
            scroll.Add(page);

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Threadlink).Assembly);
            bool live = Threadlink.TryGetSingleton(out var core);

            var hero = new VisualElement();
            hero.AddToClassList("tl-hero");
            var banner = Brand("ThreadlinkBanner.png");
            if (banner != null)
            {
                var image = new Image { image = banner, scaleMode = ScaleMode.ScaleToFit, tooltip = "Threadlink. Weaving worlds together." };
                image.AddToClassList("tl-hero__banner");
                hero.Add(image);
            }
            else
            {
                var title = new Label("Threadlink");
                title.AddToClassList("tl-title");
                hero.Add(title);
            }
            var chips = new VisualElement();
            chips.AddToClassList("tl-hero__chips");
            chips.Add(Chip("Version " + (package != null ? package.version : "unknown"), false));
            chips.Add(Chip(live ? "Live · " + core.Subsystems.Count + " subsystems" : "Deploys when Play mode starts", live));
            chips.Add(Chip(ThreadlinkSettingsAssets.Types.Count + " settings pages", false));
            hero.Add(chips);
            page.Add(hero);

            page.Add(WorkspaceUI.Text("The core has no settings of its own: subsystems register and deploy automatically before the first scene. Each package's engineering settings have a page below; settings without an asset use their defaults."));
            page.Add(WorkspaceUI.Heading("Session"));
            page.Add(
                WorkspaceUI.Row(
                    "Threadlink core",
                    live ? "Deployed with " + core.Subsystems.Count + " subsystems" : "Not deployed (Edit mode)",
                    "Runtime Monitor",
                    WorkspaceWindow.OpenRuntime
                )
            );

            page.Add(WorkspaceUI.Heading("Settings"));
            var types = ThreadlinkSettingsAssets.Types;
            if (types.Count == 0)
                page.Add(WorkspaceUI.Text("No installed package has engineering settings."));
            foreach (var type in types)
            {
                var probe = (ThreadlinkSettings)ScriptableObject.CreateInstance(type);
                string name = probe.PageName;
                Object.DestroyImmediate(probe);
                var settings = ThreadlinkSettingsAssets.Get(type, false);
                string problem = settings != null ? settings.Validate() : null;
                string owner = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly)?.displayName ?? "Project";
                string state = settings == null ? "Defaults (no asset yet)" : problem ?? "Configured";
                page.Add(
                    WorkspaceUI.Row(
                        name + "  ·  " + owner,
                        state,
                        "Open",
                        () => SettingsService.OpenProjectSettings("Project/Threadlink/" + name),
                        problem != null ? IssueSeverity.Warning : IssueSeverity.Info
                    )
                );
            }

            page.Add(WorkspaceUI.Heading("Tools"));
            page.Add(
                WorkspaceUI.Buttons(
                    ("Workspace", () => WorkspaceWindow.Open()),
                    ("Validation", () => WorkspaceWindow.Open().Show(typeof(ValidationPage))),
                    ("Documentation", () => Application.OpenURL(package != null && !string.IsNullOrEmpty(package.documentationUrl) ? package.documentationUrl : "https://threadforge.gitbook.io/threadlink-documentation/"))
                )
            );

            var footer = new VisualElement();
            footer.AddToClassList("tl-footer");
            var logo = Brand("ThreadforgeLogo.png");
            if (logo != null)
            {
                var mark = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit, tooltip = "Threadforge" };
                mark.AddToClassList("tl-footer__logo");
                footer.Add(mark);
            }
            var credit = new VisualElement();
            credit.AddToClassList("tl-footer__text");
            var maker = new Label("Threadlink is developed and maintained by Threadforge.");
            maker.AddToClassList("tl-footer__title");
            credit.Add(maker);
            var licence = new Label("MIT licensed · " + (package != null ? package.name + " " + package.version : "com.threadforge.threadlink"));
            licence.AddToClassList("tl-footer__detail");
            credit.Add(licence);
            footer.Add(credit);
            page.Add(footer);
        }

        private static Texture2D Brand(string file) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.threadforge.threadlink/Editor/Theme/Images/" + file);

        private static Label Chip(string text, bool live)
        {
            var chip = new Label(text);
            chip.AddToClassList("tl-chip");
            chip.EnableInClassList("tl-chip--live", live);
            return chip;
        }
    }

    /// <summary>Settings assets are read-only in the Inspector: they are edited in Project Settings.</summary>
    [CustomEditor(typeof(ThreadlinkSettings), true)]
    internal sealed class ThreadlinkSettingsInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var settings = (ThreadlinkSettings)target;
            root.Add(new HelpBox("Engineering settings for Threadlink " + settings.PageName + ". Edit them in Project Settings ▸ Threadlink ▸ " + settings.PageName + ".", HelpBoxMessageType.Info));
            root.Add(new Button(() => SettingsService.OpenProjectSettings("Project/Threadlink/" + settings.PageName)) { text = "Open Project Settings" });
            return root;
        }
    }

    /// <summary>Keeps settings assets preloaded in every build.</summary>
    internal sealed class ThreadlinkSettingsBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => ThreadlinkSettingsAssets.Preload();
    }

    /// <summary>Reports problems in the project's settings.</summary>
    public sealed class ThreadlinkSettingsValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Settings";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            foreach (var type in ThreadlinkSettingsAssets.Types)
            {
                var settings = ThreadlinkSettingsAssets.Get(type, false);
                if (settings == null)
                    continue;
                string problem = settings.Validate();
                if (problem != null)
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, settings.PageName + ": " + problem, settings));
            }
        }
    }
}
