namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Shared;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Aura Config")]
    public sealed class AuraConfig : ScriptableObject
    {
        internal ThreadlinkIDs.Addressables.Assets NavClipPointer => navigationClipPointer;
        internal ThreadlinkIDs.Addressables.Assets ConfirmClipPointer => confirmClipPointer;
        internal ThreadlinkIDs.Addressables.Assets CancelClipPointer => cancelClipPointer;
        internal float VolumeFadeSpeed => volumeFadeSpeed;

        [SerializeField] private float volumeFadeSpeed = 8f;

        [Space(10)]

        [SerializeField] private ThreadlinkIDs.Addressables.Assets navigationClipPointer = default;
        [SerializeField] private ThreadlinkIDs.Addressables.Assets confirmClipPointer = default;
        [SerializeField] private ThreadlinkIDs.Addressables.Assets cancelClipPointer = default;
    }
}
