namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Addressables;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Aura Config")]
    public sealed class AuraConfig : ScriptableObject
    {
        internal GroupedAssetPointer NavClipPointer => navigationClipPointer;
        internal GroupedAssetPointer ConfirmClipPointer => confirmClipPointer;
        internal GroupedAssetPointer CancelClipPointer => cancelClipPointer;
        internal float VolumeFadeSpeed => volumeFadeSpeed;

        [SerializeField] private float volumeFadeSpeed = 8f;

        [Space(10)]

        [SerializeField] private GroupedAssetPointer navigationClipPointer = null;
        [SerializeField] private GroupedAssetPointer confirmClipPointer = null;
        [SerializeField] private GroupedAssetPointer cancelClipPointer = null;
    }
}
