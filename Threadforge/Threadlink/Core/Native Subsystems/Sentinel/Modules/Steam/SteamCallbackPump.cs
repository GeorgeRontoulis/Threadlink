namespace Threadlink.SentinelModules.Steam
{
    using Steamworks;
    using UnityEngine;

    internal sealed class SteamCallbackPump : MonoBehaviour
    {
        internal static SteamCallbackPump Create()
        {
            var gameObject = new GameObject("[Threadlink Sentinel Steam]")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<SteamCallbackPump>();
        }

        private void Update()
        {
            if (CallbackDispatcher.IsInitialized)
                SteamAPI.RunCallbacks();
        }
    }
}
