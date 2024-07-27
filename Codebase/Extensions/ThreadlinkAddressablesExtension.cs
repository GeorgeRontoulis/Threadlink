namespace Threadlink.Extensions.Addressables
{
	using Core;
	using UnityEngine;
	using Utilities.Addressables;

	public abstract class ThreadlinkAddressablesExtension : LinkableAsset
	{
		public abstract void SearchForAddressablePrefab<AddressableType, PrefabType>(string prefabID, out AddressableType result)
		where AddressableType : AddressablePrefab<PrefabType> where PrefabType : Component;

		public abstract void SearchForAddressableAsset<AddressableType, AssetType>(string assetID, out AddressableType result)
		where AddressableType : AddressableAsset<AssetType> where AssetType : Object;
	}
}