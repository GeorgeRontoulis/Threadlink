namespace Threadlink.Addressables.Extensions
{
	using Core;
	using UnityEngine;

	public abstract class ThreadlinkAddressablesExtension : LinkableAssetSingleton<ThreadlinkAddressablesExtension>
	{
		public abstract bool TryGetAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component;

		public abstract bool TryGetAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : Object;
	}
}