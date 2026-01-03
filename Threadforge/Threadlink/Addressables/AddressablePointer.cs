namespace Threadlink.Addressables
{
    using System;
    using UnityEngine;

    [Serializable]
    public abstract class AddressablePointer
    {
        public int IndexInDatabase => indexInDatabase;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [SerializeField] protected int indexInDatabase = -1;
    }

    [Serializable]
    public sealed class ScenePointer : AddressablePointer { }

    [Serializable]
    public sealed class GroupedAssetPointer : AddressablePointer
    {
        public AssetGroups Group => group;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [SerializeField] private AssetGroups group = 0;
    }
}