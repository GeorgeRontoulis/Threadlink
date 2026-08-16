namespace Threadlink.Core
{
    using Generated;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;

    public sealed partial class Threadlink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetReference(ThreadlinkIDs.Addressables.Assets assetID, out AssetReference result)
        {
            return UserConfig.TryGetAssetReference(assetID, out result) && ValidateAssetReference(result, assetID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPrefabReference(ThreadlinkIDs.Addressables.Prefabs prefabID, out AssetReferenceGameObject result)
        {
            return UserConfig.TryGetPrefabReference(prefabID, out result) && ValidateAssetReference(result, prefabID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneReference(ThreadlinkIDs.Addressables.Scenes sceneID, out SceneAssetReference result)
        {
            return UserConfig.TryGetSceneReference(sceneID, out result) && ValidateAssetReference(result, sceneID);
        }
    }
}
