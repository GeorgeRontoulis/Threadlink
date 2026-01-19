namespace Threadlink.Core
{
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;

    public sealed partial class Threadlink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAssetReference(AssetIDs assetID, out AssetReference result)
        {
            if (Instance.UserConfig.TryGetAssetRefs(out var assets))
                return ValidateAssetReferenceRequest(assets, (uint)assetID, out result);

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPrefabReference(PrefabIDs prefabID, out AssetReferenceGameObject result)
        {
            if (Instance.UserConfig.TryGetPrefabRefs(out var prefabs))
                return ValidateAssetReferenceRequest(prefabs, (uint)prefabID, out result);

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSceneReference(SceneIDs sceneID, out SceneAssetReference result)
        {
            if (Instance.UserConfig.TryGetSceneRefs(out var scenes))
                return ValidateAssetReferenceRequest(scenes, (uint)sceneID, out result);

            result = null;
            return false;
        }
    }
}
