namespace Threadlink.Addressables
{
	using System;

#if UNITY_EDITOR
	using Editor.Attributes;
#endif

	[Serializable]
	public sealed class AddressablePointer
	{
#if UNITY_EDITOR
		[AddressableAssetButton]
#endif
		public string assetAddress = string.Empty;
	}
}