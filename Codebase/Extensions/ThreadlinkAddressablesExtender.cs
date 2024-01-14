namespace Threadlink.Extensions.Addressables
{
	using UnityEngine;
	using Utilities.Addressables;

	public abstract class ThreadlinkAddressablesExtender : ScriptableObject
	{
		public abstract AddressableType SearchForAddressablePrefab<AddressableType, PrefabType>(string prefabID)
		where AddressableType : AddressablePrefab<PrefabType> where PrefabType : Component;

		public abstract AddressableType SearchForAddressableAsset<AddressableType, AssetType>(string prefabID)
		where AddressableType : AddressableAsset<AssetType> where AssetType : Object;
	}
}