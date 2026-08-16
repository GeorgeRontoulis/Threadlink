namespace Threadlink.Core
{
    using Generated;
    using NativeSubsystems.Scribe;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;

    public sealed partial class Threadlink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Scenes sceneID) => TryGetSceneReference(sceneID, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Assets assetID) => TryGetAssetReference(assetID, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckIDValidity(ThreadlinkIDs.Addressables.Prefabs prefabID) => TryGetPrefabReference(prefabID, out _);

        private bool ValidateAssetReference<TReference, TID>(TReference reference, TID id) where TReference : AssetReference
        {
            if (reference == null)
            {
                this.Send("No Asset Reference is mapped to ", id, "!").ToUnityConsole(DebugType.Error);
                return false;
            }

            if (!reference.RuntimeKeyIsValid())
            {
                this.Send("RuntimeKey of ", id, ", ", reference.RuntimeKey, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            return true;
        }
    }
}
