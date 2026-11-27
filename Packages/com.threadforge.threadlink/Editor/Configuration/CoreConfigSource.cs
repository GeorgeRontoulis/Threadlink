namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>The core's configuration assets: every engineering settings asset, and Addressables' settings.</summary>
    public sealed class CoreConfigSource : ConfigSource
    {
        /// <inheritdoc />
        public override IEnumerable<ConfigAsset> Assets =>
            ThreadlinkSettingsAssets
                .Types.Select(type => (ConfigAsset)new SettingsConfig(type))
                .Append(new AddressablesConfig());

        private sealed class SettingsConfig : ConfigAsset
        {
            private readonly Type type;

            public SettingsConfig(Type type)
                : base(ThreadlinkSettingsAssets.Location(type)) => this.type = type;

            public override string Description =>
                "Engineering settings, edited in Project Settings ▸ Threadlink ▸ "
                + ThreadlinkSettingsAssets.PageName(type)
                + ".";

            public override Object Find() => ThreadlinkSettingsAssets.Get(type, false);

            protected override Object CreateIn(string folder)
            {
                var settings = ThreadlinkSettingsAssets.Get(type, true);
                ThreadlinkSettingsAssets.Preload();
                return settings;
            }
        }

        private sealed class AddressablesConfig : ConfigAsset
        {
            public AddressablesConfig()
                : base(
                    new AssetLocation(
                        "unity.addressables.settings",
                        "Addressables Settings",
                        "Content",
                        ConfigRoot.Engineering
                    )
                ) { }

            public override string Description =>
                "Unity Addressables' settings and groups, which content loading and the Content mapping use. Its folder moves as a whole.";

            public override Object Find() => AddressableAssetSettingsDefaultObject.Settings;

            protected override Object CreateIn(string folder)
            {
                string data = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/AddressableAssetsData"
                );
                var settings = AddressableAssetSettings.Create(
                    data,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true,
                    true
                );
                // Addressables creates its default object in Assets/AddressableAssetsData unless one is registered; keep it here.
                if (
                    !EditorBuildSettings.TryGetConfigObject(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                        out AddressableAssetSettingsDefaultObject pointer
                    )
                    || pointer == null
                )
                {
                    pointer =
                        ScriptableObject.CreateInstance<AddressableAssetSettingsDefaultObject>();
                    AssetDatabase.CreateAsset(
                        pointer,
                        AssetLocations.EnsureFolder(data) + "/DefaultObject.asset"
                    );
                    EditorBuildSettings.AddConfigObject(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                        pointer,
                        true
                    );
                }
                AddressableAssetSettingsDefaultObject.Settings = settings;
                return settings;
            }

            // The settings' own folder, with its groups, schemas and builders. ConfigFolder can lag behind a move.
            protected override string MovedPath(string assetPath)
            {
                string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                return string.IsNullOrEmpty(folder) || folder == "Assets" ? assetPath : folder;
            }
        }
    }
}
