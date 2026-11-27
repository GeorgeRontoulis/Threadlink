namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Localization;
    using UnityEditor.Localization.UI;
    using UnityEngine.Localization;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>Unity Localization's settings, locales and string tables, and the folders new ones are created in.</summary>
    public sealed class LocalizationConfigSource : ConfigSource
    {
        /// <inheritdoc />
        public override IEnumerable<ConfigAsset> Assets =>
            new ConfigAsset[] { new SettingsConfig() };

        /// <inheritdoc />
        public override IEnumerable<AssetLocation> Locations =>
            new[] { LocalizationSetup.LocalesLocation, LocalizationSetup.StringTablesLocation };

        private sealed class SettingsConfig : ConfigAsset
        {
            public SettingsConfig()
                : base(LocalizationSetup.SettingsLocation) { }

            public override string Description =>
                "Unity Localization's active settings: available locales, startup language selection and table loading.";

            public override Object Find() => LocalizationEditorSettings.ActiveLocalizationSettings;

            protected override Object CreateIn(string folder)
            {
                LocalizationSetup.Run();
                return LocalizationEditorSettings.ActiveLocalizationSettings;
            }
        }
    }

    /// <summary>
    /// Localization in the Workspace: setup, locales, string tables and their folders. Threadlink's player-visible text,
    /// such as interaction prompts, is string table entries.
    /// </summary>
    public sealed class LocalizationPage : WorkspacePage
    {
        /// <inheritdoc />
        public override string Title => "Localization";

        /// <inheritdoc />
        public override int Order => 45;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            Action changed = context.Refresh;
            root.Add(
                WorkspaceUI.Text(
                    "Player-visible text is Unity Localization string table entries: interaction prompts, the Starter's and Showcase's text, and your own LocalizedString fields. Setup creates what is missing and registers every string table for loading; importing a Threadlink sample runs it."
                )
            );
            bool ready = LocalizationSetup.IsReady;
            root.Add(
                WorkspaceUI.Row(
                    "Setup",
                    ready
                        ? "Ready"
                        : "Not set up: settings, the English locale or table registration is missing.",
                    ready ? "Run again" : "Set up",
                    () =>
                    {
                        LocalizationSetup.Run();
                        changed();
                    },
                    ready ? IssueSeverity.Info : IssueSeverity.Warning
                )
            );
            root.Add(
                WorkspaceUI.Buttons(
                    (
                        "Localization Settings",
                        () => context.OpenProjectSettings("Project/Localization")
                    ),
                    ("Tables window", () => LocalizationTablesWindow.ShowWindow()),
                    (
                        "Scene Controls",
                        () =>
                            context.OpenMenu("Window/Asset Management/Localization Scene Controls")
                    )
                )
            );

            root.Add(WorkspaceUI.Heading("Configuration"));
            root.Add(ConfigUI.Category(context, "Localization", changed));

            var locales = LocalizationEditorSettings
                .GetLocales()
                .OrderBy(locale => locale.Identifier.Code, StringComparer.Ordinal)
                .ToList();
            root.Add(WorkspaceUI.Heading($"Locales ({locales.Count})"));
            string localesFolder = LocalizationSetup.LocalesLocation.Folder;
            foreach (var locale in locales)
                root.Add(
                    MovableRow(
                        context,
                        locale,
                        locale.LocaleName + " · " + locale.Identifier.Code,
                        localesFolder,
                        changed
                    )
                );
            root.Add(AddLocaleRow(locales, changed));

            var tables = LocalizationEditorSettings
                .GetStringTableCollections()
                .OrderBy(collection => collection.TableCollectionName, StringComparer.Ordinal)
                .ToList();
            root.Add(WorkspaceUI.Heading($"String tables ({tables.Count})"));
            foreach (var collection in tables)
            {
                var target = collection;
                int entries =
                    collection.SharedData != null ? collection.SharedData.Entries.Count : 0;
                bool registered = LocalizationSetup.IsRegistered(collection);
                var row = WorkspaceUI.Row(
                    collection.TableCollectionName,
                    $"{collection.StringTables.Count} locale(s) · {entries} entries · {AssetDatabase.GetAssetPath(collection)}"
                        + (registered ? string.Empty : " · not registered for loading"),
                    "Open",
                    () => LocalizationTablesWindow.ShowWindow(target),
                    registered ? IssueSeverity.Info : IssueSeverity.Warning
                );
                row.Add(new Button(() => context.Select(target)) { text = "Select" });
                root.Add(row);
            }
            root.Add(NewTableRow(context, changed));
            return root;
        }

        private static VisualElement MovableRow(
            WorkspaceContext context,
            Object asset,
            string title,
            string folder,
            Action changed
        )
        {
            string path = AssetDatabase.GetAssetPath(asset);
            var row = WorkspaceUI.Row(title, path, "Select", () => context.Select(asset));
            if (
                path.StartsWith("Assets/", StringComparison.Ordinal)
                && !AssetLocations.IsIn(path, folder)
            )
                row.Add(
                    new Button(() =>
                    {
                        string error = AssetDatabase.MoveAsset(
                            path,
                            AssetDatabase.GenerateUniqueAssetPath(
                                AssetLocations.EnsureFolder(folder)
                                    + "/"
                                    + System.IO.Path.GetFileName(path)
                            )
                        );
                        if (!string.IsNullOrEmpty(error))
                            EditorUtility.DisplayDialog(
                                "Could not move " + asset.name,
                                error,
                                "OK"
                            );
                        changed();
                    })
                    {
                        text = "Move to folder",
                        tooltip = "Move it to " + folder + ".",
                    }
                );
            return row;
        }

        private static VisualElement AddLocaleRow(List<Locale> existing, Action changed)
        {
            var codes = new HashSet<string>(
                existing.Select(locale => locale.Identifier.Code),
                StringComparer.OrdinalIgnoreCase
            );
            var choices = CultureInfo
                .GetCultures(CultureTypes.AllCultures)
                .Where(culture =>
                    !string.IsNullOrEmpty(culture.Name) && !codes.Contains(culture.Name)
                )
                .OrderBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase)
                .Select(culture => culture.EnglishName + " (" + culture.Name + ")")
                .ToList();
            var row = new VisualElement();
            row.AddToClassList("tl-row");
            var picker = new DropdownField("Add locale", choices, 0)
            {
                tooltip =
                    "The locale is created in the Locales folder and gets a table in every string table collection.",
            };
            picker.style.flexGrow = 1;
            row.Add(picker);
            row.Add(
                new Button(() =>
                {
                    string choice = picker.value;
                    if (string.IsNullOrEmpty(choice))
                        return;
                    string code = choice.Substring(choice.LastIndexOf('(') + 1).TrimEnd(')');
                    LocalizationSetup.AddLocale(code);
                    changed();
                })
                {
                    text = "Add",
                }
            );
            return row;
        }

        private static VisualElement NewTableRow(WorkspaceContext context, Action changed)
        {
            var row = new VisualElement();
            row.AddToClassList("tl-row");
            var name = new TextField("New string table")
            {
                tooltip = "Created in the String Tables folder with a table for every locale.",
            };
            name.style.flexGrow = 1;
            row.Add(name);
            row.Add(
                new Button(() =>
                {
                    try
                    {
                        var collection = LocalizationSetup.CreateStringTable(name.value);
                        context.Select(collection);
                    }
                    catch (ArgumentException error)
                    {
                        EditorUtility.DisplayDialog(
                            "Could not create the string table",
                            error.Message,
                            "OK"
                        );
                    }
                    changed();
                })
                {
                    text = "Create",
                }
            );
            return row;
        }
    }
}
