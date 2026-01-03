namespace Threadlink.Core.NativeSubsystems.Sentinel
{
#if THREADLINK_SENTINEL_XBOX
    using Unity.XGamingRuntime;
    using UnityEngine;

    public static class XBLAchievement
    {
        public static void Unlock(string achievementID, XUserHandle userHandle,
        XblContextHandle xblContextHandle, SDK.XBL.XblAchievementsUpdateAchievementResult onUnlockedCallback)
        {
            if (Application.isEditor) return;

            SDK.XUserGetId(userHandle, out var xuid);

            // This API will work even when offline.  Offline updates will be posted by the system when connection is
            // re-established even if the title isn't running. If the achievement has already been unlocked or the progress
            // value is less than or equal to what is currently recorded on the server HTTP_E_STATUS_NOT_MODIFIED (0x80190130L)
            // will be returned.
            SDK.XBL.XblAchievementsUpdateAchievementAsync(xblContextHandle, xuid, achievementID, 100, onUnlockedCallback);
        }
    }
#endif
}
