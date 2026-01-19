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
        public static bool CheckIDValidity(SceneIDs sceneID)
        {
            return Instance.UserConfig.TryGetSceneRefs(out var scenes) && ValidateAssetReferenceRequest(scenes, (uint)sceneID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckIDValidity(AssetIDs assetID)
        {
            return Instance.UserConfig.TryGetAssetRefs(out var assets) && ValidateAssetReferenceRequest(assets, (uint)assetID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckIDValidity(PrefabIDs prefabID)
        {
            return Instance.UserConfig.TryGetPrefabRefs(out var prefabs) && ValidateAssetReferenceRequest(prefabs, (uint)prefabID, out _);
        }

        private static bool ValidateAssetReferenceRequest<T>(ReadOnlySpan<T> databaseView, uint index, out T reference)
        where T : AssetReference
        {
            reference = null;

            if (!index.IsWithinBoundsOf(databaseView))
            {
                Instance.Send("The Asset Reference Index ", index, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            var assetReference = databaseView[(int)index];

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
