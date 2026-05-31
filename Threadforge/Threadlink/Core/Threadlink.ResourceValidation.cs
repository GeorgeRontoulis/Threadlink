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
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Scenes sceneID)
        {
            return UserConfig.TryGetSceneRefs(out var scenes) && ValidateAssetReferenceRequest(scenes, (int)sceneID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Assets assetID)
        {
            return UserConfig.TryGetAssetRefs(out var assets) && ValidateAssetReferenceRequest(assets, (int)assetID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Prefabs prefabID)
        {
            return UserConfig.TryGetPrefabRefs(out var prefabs) && ValidateAssetReferenceRequest(prefabs, (int)prefabID, out _);
        }

        private bool ValidateAssetReferenceRequest<T>(ReadOnlySpan<T> databaseView, int index, out T reference)
        where T : AssetReference
        {
            reference = null;

            if (!index.IsWithinBoundsOf(databaseView))
            {
                this.Send("The Asset Reference Index ", index, " is invalid!").ToUnityConsole(DebugType.Warning);
                return false;
            }

            var assetReference = databaseView[index];

            if (assetReference == null)
            {
                this.Send(assetReference, " at index ", index, " is NULL!").ToUnityConsole(DebugType.Error);
                return false;
            }
            else if (!assetReference.RuntimeKeyIsValid())
            {
                this.Send("RuntimeKey of ", assetReference, ", ", assetReference.RuntimeKey, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            reference = assetReference;
            return true;
        }
    }
}
