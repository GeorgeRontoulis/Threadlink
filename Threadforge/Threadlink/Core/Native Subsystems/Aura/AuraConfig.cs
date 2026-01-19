namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Shared;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Aura Config")]
    public sealed class AuraConfig : ScriptableObject
    {
        internal AssetIDs NavClipPointer => navigationClipPointer;
        internal AssetIDs ConfirmClipPointer => confirmClipPointer;
        internal AssetIDs CancelClipPointer => cancelClipPointer;
        internal float VolumeFadeSpeed => volumeFadeSpeed;

        [SerializeField] private float volumeFadeSpeed = 8f;

        [Space(10)]

        [SerializeField] private AssetIDs navigationClipPointer = default;
        [SerializeField] private AssetIDs confirmClipPointer = default;
        [SerializeField] private AssetIDs cancelClipPointer = default;
    }
}
