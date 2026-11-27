namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.Localization;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.Localization.Settings;

    /// <summary>
    /// Prepares the project for Unity Localization: active Localization settings, the English locale, and every string
    /// table collection registered for loading. Threadlink's player-visible text, such as interaction prompts and the
    /// Starter's and Showcase's tables, is localized through it. The Workspace's Localization page runs it, and
    /// importing a Threadlink sample that brings string tables runs it automatically.
    /// </summary>
    public static class LocalizationSetup
    {
        /// <summary>Where new Localization settings are created.</summary>
        public static readonly AssetLocation SettingsLocation = new AssetLocation(
            "unity.localization.settings",
            "Localization Settings",
            "Localization",
            ConfigRoot.Engineering
        );

        /// <summary>Where new locales are created.</summary>
        public static readonly AssetLocation LocalesLocation = new AssetLocation(
            "unity.localization.locales",
            "Locales",
            "Localization",
            ConfigRoot.Design
        );

        /// <summary>Where new string table collections are created.</summary>
        public static readonly AssetLocation StringTablesLocation = new AssetLocation(
            "unity.localization.string-tables",
            "String Tables",
            "Localization",
            ConfigRoot.Design
        );

        /// <summary>
        /// Whether the project has active Localization settings with the English locale, and every string table is
        /// registered for loading.
        /// </summary>
        public static bool IsReady =>
            LocalizationEditorSettings.ActiveLocalizationSettings != null
            && LocalizationEditorSettings.GetLocale("en") != null
            && LocalizationEditorSettings.GetStringTableCollections().All(IsRegistered);

        /// <summary>Whether every table of <paramref name="collection"/> is registered for loading.</summary>
        public static bool IsRegistered(StringTableCollection collection)
        {
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            return addressables != null
                && collection.StringTables.All(table =>
                    addressables.FindAssetEntry(
                        AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table))
                    ) != null
                );
        }

        /// <summary>
        /// Creates what is missing and registers string tables imported with samples, which Localization loads through
        /// Addressables. Safe to run again.
        /// </summary>
        public static void Run()
        {
            // Importing a collection makes Localization create the locales it uses next to that collection; one created
            // beside a Threadlink sample's table moves to the locales folder, so removing the sample keeps the language.
            var english = FindLocale("en");
            if (english != null && IsBesideThreadlinkTable(AssetDatabase.GetAssetPath(english)))
                AssetDatabase.MoveAsset(
                    AssetDatabase.GetAssetPath(english),
                    AssetLocations.NewAssetPath(
                        LocalesLocation,
                        Path.GetFileName(AssetDatabase.GetAssetPath(english))
                    )
                );
            if (english == null)
            {
                english = Locale.CreateLocale("en");
                AssetDatabase.CreateAsset(
                    english,
                    AssetLocations.NewAssetPath(LocalesLocation, english.name + ".asset")
                );
            }
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Localization Settings";
                AssetDatabase.CreateAsset(
                    settings,
                    AssetLocations.NewAssetPath(SettingsLocation, "Localization Settings.asset")
                );
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }
            if (LocalizationEditorSettings.GetLocale("en") == null)
                LocalizationEditorSettings.AddLocale(english);
            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
                collection.RefreshAddressables();
            // A table imported before its locale existed leaves an empty group behind once it moves to the locale's.
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables != null)
                foreach (
                    var group in addressables
                        .groups.Where(group =>
                            group != null
                            && group.entries.Count == 0
                            && group.Name.StartsWith(
                                "Localization-String-Tables-",
                                StringComparison.Ordinal
                            )
                        )
                        .ToList()
                )
                    addressables.RemoveGroup(group);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Adds the locale with <paramref name="code"/>, such as "fr" or "pt-BR", in the locales folder, and a table for
        /// it to every string table collection. Returns the existing locale when the project has one.
        /// </summary>
        /// <exception cref="ArgumentException">The code is empty.</exception>
        public static Locale AddLocale(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A locale code is required.", nameof(code));
            code = code.Trim();
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
                Run();
            var locale = LocalizationEditorSettings.GetLocale(code) ?? FindLocale(code);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(
                    locale,
                    AssetLocations.NewAssetPath(LocalesLocation, locale.name + ".asset")
                );
            }
            if (LocalizationEditorSettings.GetLocale(locale.Identifier.Code) == null)
                LocalizationEditorSettings.AddLocale(locale);
            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
                if (collection.GetTable(locale.Identifier) == null)
                    collection.AddNewTable(locale.Identifier);
            AssetDatabase.SaveAssets();
            return locale;
        }

        /// <summary>Creates a string table collection named <paramref name="name"/> for every locale, in the string tables folder.</summary>
        /// <exception cref="ArgumentException">The name is empty or a collection already has it.</exception>
        public static StringTableCollection CreateStringTable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A table name is required.", nameof(name));
            name = name.Trim();
            if (LocalizationEditorSettings.GetStringTableCollection(name) != null)
                throw new ArgumentException(
                    "A string table named '" + name + "' already exists.",
                    nameof(name)
                );
            if (!IsReady)
                Run();
            var collection = LocalizationEditorSettings.CreateStringTableCollection(
                name,
                AssetLocations.EnsureFolder(StringTablesLocation.Folder)
            );
            AssetDatabase.SaveAssets();
            return collection;
        }

        private static Locale FindLocale(string code) =>
            AssetDatabase
                .FindAssets("t:Locale")
                .Select(guid =>
                    AssetDatabase.LoadAssetAtPath<Locale>(AssetDatabase.GUIDToAssetPath(guid))
                )
                .FirstOrDefault(locale => locale != null && locale.Identifier.Code == code);

        private static bool IsBesideThreadlinkTable(string path)
        {
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return LocalizationEditorSettings
                .GetStringTableCollections()
                .Where(collection =>
                    collection.TableCollectionName.StartsWith(
                        "Threadlink ",
                        StringComparison.Ordinal
                    )
                )
                .Any(collection =>
                    Path.GetDirectoryName(AssetDatabase.GetAssetPath(collection))
                        ?.Replace('\\', '/') == folder
                );
        }

        /// <summary>Sets Localization up once a Threadlink sample's string tables arrive in the project.</summary>
        private sealed class SampleImport : AssetPostprocessor
        {
            // Survives the domain reload that often follows an import.
            private const string Pending = "Threadforge.Threadlink.LocalizationSetup.Pending";

            private static void OnPostprocessAllAssets(
                string[] imported,
                string[] deleted,
                string[] moved,
                string[] movedFrom
            )
            {
                if (!imported.Any(IsThreadlinkTable))
                    return;
                SessionState.SetBool(Pending, true);
                Schedule();
            }

            [InitializeOnLoadMethod]
            private static void Resume()
            {
                if (SessionState.GetBool(Pending, false))
                    Schedule();
            }

            private static void Schedule()
            {
                EditorApplication.delayCall -= SetUpIfNeeded;
                EditorApplication.delayCall += SetUpIfNeeded;
            }

            private static bool IsThreadlinkTable(string path) =>
                path.StartsWith("Assets/", StringComparison.Ordinal)
                && Path.GetFileName(path).StartsWith("Threadlink ", StringComparison.Ordinal)
                && path.EndsWith(" Shared Data.asset", StringComparison.Ordinal);

            private static void SetUpIfNeeded()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    Schedule();
                    return;
                }
                SessionState.EraseBool(Pending);
                bool wasReady = IsReady;
                Run();
                if (!wasReady)
                    Debug.Log(
                        "Threadlink set up Unity Localization for the imported string tables ("
                            + SettingsLocation.Folder
                            + ")."
                    );
            }
        }
    }
}
