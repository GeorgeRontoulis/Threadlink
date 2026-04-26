namespace Threadlink.Core
{
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;
    using Utilities.Collections;

    public sealed partial class Threadlink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckIDValidity(ThreadlinkIDs.Addressables.Scenes sceneID)
        {
            return Instance.UserConfig.TryGetSceneRefs(out var scenes) && ValidateAssetReferenceRequest(scenes, (int)sceneID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckIDValidity(ThreadlinkIDs.Addressables.Assets assetID)
        {
            return Instance.UserConfig.TryGetAssetRefs(out var assets) && ValidateAssetReferenceRequest(assets, (int)assetID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckIDValidity(ThreadlinkIDs.Addressables.Prefabs prefabID)
        {
            return Instance.UserConfig.TryGetPrefabRefs(out var prefabs) && ValidateAssetReferenceRequest(prefabs, (int)prefabID, out _);
        }

        private static bool ValidateAssetReferenceRequest<T>(ReadOnlySpan<T> databaseView, int index, out T reference)
        where T : AssetReference
        {
            reference = null;

            if (!index.IsWithinBoundsOf(databaseView))
            {
                Instance.Send("The Asset Reference Index ", index, " is invalid!").ToUnityConsole(DebugType.Warning);
                return false;
            }

            var assetReference = databaseView[index];

            if (assetReference == null)
            {
                Instance.Send(assetReference, " at index ", index, " is NULL!").ToUnityConsole(DebugType.Error);
                return false;
            }
            else if (!assetReference.RuntimeKeyIsValid())
            {
                Instance.Send("RuntimeKey of ", assetReference, ", ", assetReference.RuntimeKey, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            reference = assetReference;
            return true;
        }
    }
}
