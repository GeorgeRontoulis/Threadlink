namespace Threadforge.Threadlink
{
    using global::Steamworks;
    using UnityEngine;

    /// <summary>Runs Steamworks.NET callbacks once per frame while its backend is started.</summary>
    internal sealed class SteamworksNetPump : MonoBehaviour
    {
        internal static SteamworksNetPump Create()
        {
            var host = new GameObject("[Threadlink Steamworks.NET]") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying)
                DontDestroyOnLoad(host);
            return host.AddComponent<SteamworksNetPump>();
        }

        private void Update()
        {
            if (CallbackDispatcher.IsInitialized)
                SteamAPI.RunCallbacks();
        }
    }
}
