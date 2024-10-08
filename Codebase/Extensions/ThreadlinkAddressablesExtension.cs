namespace Threadlink.Extensions.Addressables
{
	using Core;
	using UnityEngine;
	using Utilities.Addressables;

	public abstract class ThreadlinkAddressablesExtension : LinkableAsset
	{
		public abstract void SearchForAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component;

		public abstract void SearchForAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : Object;
	}
}