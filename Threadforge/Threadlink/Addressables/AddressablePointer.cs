namespace Threadlink.Addressables
{
    using System;
    using UnityEngine;

    [Serializable]
    public abstract class AddressablePointer
    {
        public int IndexInDatabase => indexInDatabase;

        [SerializeField] protected int indexInDatabase = -1;
    }

    [Serializable]
    public sealed class ScenePointer : AddressablePointer { }

    [Serializable]
    public sealed class GroupedAssetPointer : AddressablePointer
    {
        public AssetGroups Group => group;

        [SerializeField] private AssetGroups group = 0;
    }
}