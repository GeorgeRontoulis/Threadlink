namespace Threadlink.Core
{
    using Generated;
    using Shared;
    using System;
    using System.Collections.Generic;
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

        /// <summary>
        /// Append to <paramref name="result"/> every mapped asset whose main type is <typeparamref name="T"/> or derives from
        /// it, without loading any: the Mapping Window records each asset's type. Returns how many were added. An asset whose
        /// type was never recorded (mapped before types were) is found once the mappings are applied again.
        /// </summary>
        public int CollectAssetIDs<T>(List<ThreadlinkIDs.Addressables.Assets> result) where T : UnityEngine.Object
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (UserConfig == null || UserConfig.TryGetAssetIDs(out var ids) is false)
                return 0;

            int added = 0;
            int count = ids.Length;

            for (int i = 0; i < count; i++)
            {
                if (UserConfig.TryGetAssetType(ids[i], out var typeName) && Type.GetType(typeName, false) is Type type
                && typeof(T).IsAssignableFrom(type))
                {
                    result.Add(ids[i]);
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        /// The current ID of an asset renamed or moved since <paramref name="oldID"/> was its ID: its tombstoned ID is an alias
        /// the Mapping Window recorded. False for a current ID, or one whose asset is gone.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveRenamedAsset(ThreadlinkIDs.Addressables.Assets oldID, out ThreadlinkIDs.Addressables.Assets currentID)
        {
            currentID = default;
            return UserConfig != null && UserConfig.TryGetAssetAlias(oldID, out currentID);
        }

        /// <summary>Adds every renamed asset's old ID with its current ID to <paramref name="result"/>. Returns how many it added.</summary>
        public int CollectRenamedAssets(List<(ThreadlinkIDs.Addressables.Assets OldID, ThreadlinkIDs.Addressables.Assets CurrentID)> result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (UserConfig == null || UserConfig.TryGetAssetAliasIDs(out var ids) is false)
                return 0;

            int added = 0;

            for (int i = 0; i < ids.Length; i++)
            {
                if (UserConfig.TryGetAssetAlias(ids[i], out var current))
                {
                    result.Add((ids[i], current));
                    added++;
                }
            }

            return added;
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
