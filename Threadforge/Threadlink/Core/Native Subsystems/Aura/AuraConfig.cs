namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Generated;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.Audio;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Aura Config")]
    public sealed class AuraConfig : ScriptableObject
    {
        internal ThreadlinkIDs.Addressables.Assets NavClipPointer => navigationClipPointer;
        internal ThreadlinkIDs.Addressables.Assets ConfirmClipPointer => confirmClipPointer;
        internal ThreadlinkIDs.Addressables.Assets CancelClipPointer => cancelClipPointer;
        internal float VolumeFadeSpeed => volumeFadeSpeed;

        [SerializeField] private AudioMixer masterAudioMixer = null;

        [Space(10)]

        [SerializeField] private float volumeFadeSpeed = 8f;

        [Space(10)]

        [SerializeField] private ThreadlinkIDs.Addressables.Assets navigationClipPointer = default;
        [SerializeField] private ThreadlinkIDs.Addressables.Assets confirmClipPointer = default;
        [SerializeField] private ThreadlinkIDs.Addressables.Assets cancelClipPointer = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetMixerValue(string parameterName, out float result)
        {
            return masterAudioMixer.GetFloat(parameterName, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TrySetMixerValue(string parameterName, float input)
        {
            return masterAudioMixer.SetFloat(parameterName, input);
        }
    }
}
