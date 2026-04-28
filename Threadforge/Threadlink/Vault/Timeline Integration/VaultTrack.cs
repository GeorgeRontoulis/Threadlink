#if THREADLINK_TIMELINE
namespace Threadlink.Vault
{
    using UnityEngine.Timeline;

    [TrackColor(0.2f, 1f, 0.6f)]
    [TrackBindingType(typeof(VaultReceiver))]
    public sealed class VaultTrack : MarkerTrack { }
}
#endif