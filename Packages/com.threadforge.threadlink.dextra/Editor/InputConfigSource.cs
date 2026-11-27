namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Input and screen configuration: the Input System's settings, Dextra's player actions and the panel settings
    /// screens render with.
    /// </summary>
    public sealed class InputConfigSource : ConfigSource
    {
        private const string Template =
            "Packages/com.unity.inputsystem/InputSystem/Editor/ProjectWideActions/ProjectWideActionsTemplate.json";

        /// <inheritdoc />
        public override IEnumerable<ConfigAsset> Assets =>
            new ConfigAsset[]
            {
                new PlayerActions(),
                new InputSettingsConfig(),
                new ProjectActions(),
                new ScreenPanel(),
            };

        private static DextraSettings Settings(bool create) =>
            (DextraSettings)ThreadlinkSettingsAssets.Get(typeof(DextraSettings), create);

        private static void Assign(Action<DextraSettings> assign)
        {
            var settings = Settings(true);
            assign(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            ThreadlinkSettingsAssets.Preload();
        }

        /// <summary>Input System default actions with fresh IDs, with maps named as Dextra's settings expect.</summary>
        internal static string PlayerActionsJson(
            string name,
            string gameplayMap,
            string interfaceMap
        )
        {
            string path = Path.GetFullPath(Template);
            string json = File.Exists(path)
                ? File.ReadAllText(path)
                : "{\"name\":\"Actions\",\"maps\":[{\"name\":\"Player\",\"id\":\"\",\"actions\":[],\"bindings\":[]},{\"name\":\"UI\",\"id\":\"\",\"actions\":[],\"bindings\":[]}],\"controlSchemes\":[]}";
            json = Regex.Replace(
                json,
                "\"id\"\\s*:\\s*\"[^\"]*\"",
                _ => "\"id\": \"" + Guid.NewGuid().ToString("D") + "\""
            );
            json = Regex.Replace(
                json,
                "\"name\"\\s*:\\s*\"ProjectWideActionsTemplate\"",
                "\"name\": \"" + name + "\""
            );
            json = Regex.Replace(
                json,
                "\"name\"\\s*:\\s*\"Player\"",
                "\"name\": \"" + gameplayMap + "\""
            );
            if (interfaceMap != "UI")
                json = Regex.Replace(
                    json,
                    "(\"maps\"[\\s\\S]*?)\"name\"\\s*:\\s*\"UI\"",
                    match => match.Groups[1].Value + "\"name\": \"" + interfaceMap + "\""
                );
            return json;
        }

        private static string MapName(string setting, string fallback) =>
            string.IsNullOrEmpty(setting) || Guid.TryParse(setting, out _) ? fallback : setting;

        private sealed class PlayerActions : ConfigAsset
        {
            public PlayerActions()
                : base(
                    new AssetLocation(
                        "threadlink.dextra.actions",
                        "Player Actions",
                        "Input",
                        ConfigRoot.Engineering
                    )
                ) { }

            public override string Description =>
                "The input actions each Threadlink player gets a copy of (Dextra ▸ Actions). They generate GameInput.";

            public override Object Find() => Settings(false)?.Actions;

            protected override Object CreateIn(string folder)
            {
                var settings = Settings(true);
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/Player Actions.inputactions"
                );
                File.WriteAllText(
                    path,
                    PlayerActionsJson(
                        Path.GetFileNameWithoutExtension(path),
                        MapName(settings.GameplayMap, "Gameplay"),
                        MapName(settings.InterfaceMap, "UI")
                    )
                );
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                Assign(dextra => dextra.Actions = actions);
                return actions;
            }
        }

        private sealed class InputSettingsConfig : ConfigAsset
        {
            public InputSettingsConfig()
                : base(
                    new AssetLocation(
                        "unity.input.settings",
                        "Input System Settings",
                        "Input",
                        ConfigRoot.Engineering
                    )
                ) { }

            public override string Description =>
                "The Input System package's settings: update mode, supported devices and defaults. Edited in Project Settings ▸ Input System Package.";

            public override Object Find()
            {
                var settings = InputSystem.settings;
                return settings != null && EditorUtility.IsPersistent(settings) ? settings : null;
            }

            protected override Object CreateIn(string folder)
            {
                var settings = Save(
                    ScriptableObject.CreateInstance<InputSettings>(),
                    folder,
                    "InputSystem.inputsettings.asset"
                );
                InputSystem.settings = settings;
                return settings;
            }
        }

        private sealed class ProjectActions : ConfigAsset
        {
            public ProjectActions()
                : base(
                    new AssetLocation(
                        "unity.input.project-actions",
                        "Project-wide Actions",
                        "Input",
                        ConfigRoot.Engineering
                    )
                ) { }

            public override string Description =>
                "The Input System's project-wide actions, enabled for the whole game. Threadlink players use Player Actions instead.";

            public override Object Find()
            {
                var actions = InputSystem.actions;
                return actions != null && EditorUtility.IsPersistent(actions) ? actions : null;
            }

            public override string CreateUnavailableReason =>
                "Threadlink players use Player Actions, one copy per player. Create project-wide actions in Project Settings ▸ Input System Package only if code outside Dextra needs them.";

            protected override Object CreateIn(string folder) =>
                throw new InvalidOperationException(CreateUnavailableReason);
        }

        private sealed class ScreenPanel : ConfigAsset
        {
            public ScreenPanel()
                : base(
                    new AssetLocation(
                        "threadlink.dextra.panel",
                        "Screen Panel Settings",
                        "Input",
                        ConfigRoot.Engineering
                    )
                ) { }

            public override string Description =>
                "The UI Toolkit panel settings screens render with (Dextra ▸ Panel): scaling, sort order and theme.";

            public override Object Find() => Settings(false)?.Panel;

            protected override bool MovesCompanions => true;

            protected override Object CreateIn(string folder)
            {
                string themePath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/Screen Theme.tss"
                );
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.match = 0.5f;
                Save(panel, folder, "Screen Panel Settings.asset");
                Assign(dextra => dextra.Panel = panel);
                return panel;
            }
        }
    }
}
