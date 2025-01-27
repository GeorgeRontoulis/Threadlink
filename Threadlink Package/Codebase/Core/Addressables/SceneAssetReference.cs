namespace Threadlink.Addressables
{
	using System;
	using UnityEngine.AddressableAssets;

	[Serializable]
	public sealed class SceneAssetReference : AssetReference
	{
		public SceneAssetReference(string guid) : base(guid) { }
		public override bool ValidateAsset(string path)
		{
#if UNITY_EDITOR
			return !string.IsNullOrEmpty(path) && path.EndsWith(".unity");
#else
			return true;
#endif
		}
	}
}