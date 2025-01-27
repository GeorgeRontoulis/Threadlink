namespace Threadlink.Addressables
{
	using System;
	using UnityEngine;

	[Serializable]
	public abstract class AddressablePointer
	{
		public int IndexInDatabase => indexInDatabase;

		[SerializeField] protected int indexInDatabase = 0;
	}

	[Serializable]
	public sealed class ScenePointer : AddressablePointer { }

	[Serializable]
	public sealed class GroupedAssetPointer : AddressablePointer
	{
		public ThreadlinkAddressableGroup Group => group;

		[SerializeField] private ThreadlinkAddressableGroup group = 0;
	}
}