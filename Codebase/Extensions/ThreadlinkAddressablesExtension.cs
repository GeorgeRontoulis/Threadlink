namespace Threadlink.Extensions.Addressables
{
	using Core;
	using UnityEngine;
	using Utilities.Addressables;

	public abstract class ThreadlinkAddressablesExtension : LinkableAsset
	{
		public abstract bool TryGetAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component;

		public abstract bool TryGetAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : Object;
	}
}