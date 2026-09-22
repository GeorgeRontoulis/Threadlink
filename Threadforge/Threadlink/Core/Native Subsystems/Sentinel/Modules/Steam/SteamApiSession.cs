namespace Threadlink.SentinelModules.Steam
{
    using System;
    using Core.NativeSubsystems.Sentinel;
    using Steamworks;
    using UnityEngine;

    /// <summary>
    /// Sole owner of the Steamworks.NET runtime lifecycle for Threadlink.
    /// </summary>
    internal sealed class SteamApiSession : IDisposable
    {
        internal uint AppID { get; }

        private SteamCallbackPump CallbackPump { get; set; }
        private bool SteamInitialized { get; set; }
        private bool Disposed { get; set; }

        private SteamApiSession(uint appID, SteamCallbackPump callbackPump)
        {
            AppID = appID;
            CallbackPump = callbackPump;
            SteamInitialized = true;
        }

        internal static SentinelResult<SteamApiSession> Acquire()
        {
            if (CallbackDispatcher.IsInitialized)
            {
                return SentinelResult<SteamApiSession>.Failure(
                    SentinelError.Conflict,
                    "Steamworks.NET is already initialized before Sentinel Steam startup. "
                        + "The Steam Sentinel module is the sole SteamAPI lifecycle owner; remove the competing initializer."
                );
            }

            var initializedHere = false;

            try
            {
                var hasDevelopmentAppID = SteamDevelopmentAppID.TryResolve(
                    out var developmentAppID
                );

#if !UNITY_EDITOR
                if (
                    SteamSentinelSettings.RestartAppIfNecessary
                    && hasDevelopmentAppID
                    && SteamAPI.RestartAppIfNecessary(new AppId_t(developmentAppID))
                )
                {
                    Application.Quit();

                    return SentinelResult<SteamApiSession>.Failure(
                        SentinelError.Cancelled,
                        "Steam requested that the application restart through the Steam client."
                    );
                }
#endif

                var init = SteamAPI.InitEx(out var initError);

                if (init is not ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    return SentinelResult<SteamApiSession>.Failure(
                        SentinelError.NativeFailure,
                        $"SteamAPI.InitEx failed ({init}): {initError}",
                        (long)init
                    );
                }

                initializedHere = true;

                var actualAppID = SteamUtils.GetAppID().m_AppId;

                if (actualAppID == 0)
                {
                    SteamAPI.Shutdown();
                    initializedHere = false;

                    return SentinelResult<SteamApiSession>.Failure(
                        SentinelError.NativeFailure,
                        "Steam initialized successfully but returned App ID 0."
                    );
                }

                if (hasDevelopmentAppID && actualAppID != developmentAppID)
                {
                    SteamAPI.Shutdown();
                    initializedHere = false;

                    return SentinelResult<SteamApiSession>.Failure(
                        SentinelError.InvalidArgument,
                        $"Steam initialized App ID {actualAppID}, but steam_appid.txt/environment expects {developmentAppID}."
                    );
                }

                return SentinelResult<SteamApiSession>.Success(
                    new SteamApiSession(actualAppID, SteamCallbackPump.Create())
                );
            }
            catch (Exception exception)
            {
                if (initializedHere)
                {
                    try
                    {
                        SteamAPI.Shutdown();
                    }
                    catch { }
                }

                return SentinelResult<SteamApiSession>.Failure(
                    SentinelError.NativeFailure,
                    $"Steam initialization failed: {exception.Message}"
                );
            }
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;

            if (CallbackPump != null)
            {
                UnityEngine.Object.Destroy(CallbackPump.gameObject);
                CallbackPump = null;
            }

            if (SteamInitialized)
            {
                SteamAPI.Shutdown();
                SteamInitialized = false;
            }
        }
    }
}
