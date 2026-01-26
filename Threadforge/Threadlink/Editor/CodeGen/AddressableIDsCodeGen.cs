namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;
    using UnityEngine.AddressableAssets;

    internal static class AddressableIDsCodeGen
    {
        private const string PLACEHOLDER = "{User Entries}";

        [MenuItem("Threadlink/CodeGen/Run Addressables CodeGen")]
        private static void RunAddressablesCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig)
            || !ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig userConfig))
                return;

            if (userConfig.TryGetSceneRefs(out var sceneRefs)
            && userConfig.TryGetAssetRefs(out var assetRefs)
            && userConfig.TryGetPrefabRefs(out var prefabRefs)
            && EnumCodeGen.TryGenerateEnum(editorConfig.SceneIDsTemplate, sceneRefs, GetEnumEntry, editorConfig.SceneIDsScript, PLACEHOLDER)
            && EnumCodeGen.TryGenerateEnum(editorConfig.AssetIDsTemplate, assetRefs, GetEnumEntry, editorConfig.AssetIDsScript, PLACEHOLDER)
            && EnumCodeGen.TryGenerateEnum(editorConfig.PrefabIDsTemplate, prefabRefs, GetEnumEntry, editorConfig.PrefabIDsScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("Addressables CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run Addressables CodeGen!").ToUnityConsole(DebugType.Error);
        }

        private static string GetEnumEntry<T>(T source) where T : AssetReference => source.editorAsset.name;
    }
}