namespace Threadlink.SentinelModules.Steam
{
    using Core.NativeSubsystems.Sentinel;
    using Steamworks;

    internal static class SteamResultMapper
    {
        internal static SentinelResult From(EResult result, string context = null)
        {
            if (result is EResult.k_EResultOK)
                return SentinelResult.Success();

            var error = result switch
            {
                EResult.k_EResultNoConnection
                or EResult.k_EResultServiceUnavailable
                or EResult.k_EResultConnectFailed
                or EResult.k_EResultRemoteDisconnect => SentinelError.NetworkUnavailable,

                EResult.k_EResultInvalidParam
                or EResult.k_EResultInvalidName
                or EResult.k_EResultValueOutOfRange => SentinelError.InvalidArgument,

                EResult.k_EResultFileNotFound => SentinelError.NotFound,

                EResult.k_EResultBusy
                or EResult.k_EResultPending
                or EResult.k_EResultRateLimitExceeded => SentinelError.Busy,

                EResult.k_EResultAccessDenied
                or EResult.k_EResultInsufficientPrivilege
                or EResult.k_EResultParentalControlRestricted => SentinelError.PermissionDenied,

                EResult.k_EResultNotLoggedOn
                or EResult.k_EResultAccountNotFound
                or EResult.k_EResultInvalidSteamID => SentinelError.AccountUnavailable,

                EResult.k_EResultCancelled => SentinelError.Cancelled,

                EResult.k_EResultDiskFull or EResult.k_EResultLimitExceeded =>
                    SentinelError.QuotaExceeded,

                EResult.k_EResultRemoteFileConflict or EResult.k_EResultLockingFailed =>
                    SentinelError.Conflict,

                EResult.k_EResultDataCorruption => SentinelError.CorruptData,

                _ => SentinelError.NativeFailure,
            };

            var message = string.IsNullOrEmpty(context)
                ? $"Steam operation failed with {result}."
                : $"{context}: {result}.";

            return SentinelResult.Failure(error, message, (long)result);
        }
    }
}
