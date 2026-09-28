namespace Threadlink.Core
{
    using Generated;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;
    using UnityEngine.SceneManagement;

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

        /// <summary>
        /// Attempt to get the loaded <see cref="Scene"/> of <paramref name="sceneID"/>.
        /// </summary>
        /// <param name="sceneID">The scene's identifier.</param>
        /// <param name="result">The loaded scene.</param>
        /// <returns><see langword="true"/> if the scene has finished loading. <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes sceneID, out Scene result)
        {
            if (TryGetSceneReference(sceneID, out var reference))
                return reference.TryGetLoadedScene(out result);

            result = default;
            return false;
        }
    }
}
