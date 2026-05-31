namespace Threadlink.Core
{
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;

    public sealed partial class Threadlink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetReference(ThreadlinkIDs.Addressables.Assets assetID, out AssetReference result)
        {
            if (UserConfig.TryGetAssetRefs(out var assets))
                return ValidateAssetReferenceRequest(assets, (int)assetID, out result);

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPrefabReference(ThreadlinkIDs.Addressables.Prefabs prefabID, out AssetReferenceGameObject result)
        {
            if (UserConfig.TryGetPrefabRefs(out var prefabs))
                return ValidateAssetReferenceRequest(prefabs, (int)prefabID, out result);

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneReference(ThreadlinkIDs.Addressables.Scenes sceneID, out SceneAssetReference result)
        {
            if (UserConfig.TryGetSceneRefs(out var scenes))
                return ValidateAssetReferenceRequest(scenes, (int)sceneID, out result);

            result = null;
            return false;
        }
    }
}
