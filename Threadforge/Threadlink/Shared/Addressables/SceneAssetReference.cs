namespace Threadlink.Shared
{
    using System;
    using UnityEngine.AddressableAssets;

    /// <summary>
    /// A special <see cref="AssetReference"/> restricted to selecting <see cref="UnityEditor.SceneAsset"/>s.
    /// </summary>
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