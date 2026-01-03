namespace Threadlink.Utilities.Localization
{
#if THREADLINK_LOCALIZATION
    using Cysharp.Threading.Tasks;
    using UnityEngine.Localization;

    public static class LocalizationUtilities
    {
        private const string INVALID_LOCALIZED_STRING = "INVALID_LOCALIZED_STRING";

        /// <summary>
        /// Safe method that checks the validity of the <paramref name="reference"/>
        /// before attempting to resolve it, preventing exceptions.
        /// </summary>
        /// <param name="reference">The target reference.</param>
        /// <returns>The resolved string.</returns>
        public static string GetSafeLocalizedString(this LocalizedString reference, string fallback = null)
        {
            if (reference == null || reference.IsEmpty)
                return string.IsNullOrEmpty(fallback) ? INVALID_LOCALIZED_STRING : fallback;

            return reference.GetLocalizedString();
        }

        /// <summary>
        /// Safe method that checks the validity of the <paramref name="reference"/>
        /// before attempting to resolve it, preventing exceptions.
        /// </summary>
        /// <param name="reference">The target reference.</param>
        /// <param name="result">The resulting localized string.</param>
        /// <returns><see langword="true"/> if the localized string has successfully been retrieved. <see langword="false"/> otherwise.</returns>
        public static bool TryGetLocalizedString(this LocalizedString reference, out string result)
        {
            if (reference == null || reference.IsEmpty)
            {
                result = null;
                return false;
            }

            result = reference.GetLocalizedString();
            return true;
        }

        /// <summary>
        /// Safe async method that checks the validity of the <paramref name="reference"/>
        /// before attempting to resolve it, preventing exceptions.
        /// </summary>
        /// <param name="reference">The target reference.</param>
        /// <returns>The resulting localized string.</returns>
        public static async UniTask<string> GetLocalizedStringAsync(this LocalizedString reference, string fallback = null)
        {
            if (reference == null || reference.IsEmpty)
                return string.IsNullOrEmpty(fallback) ? INVALID_LOCALIZED_STRING : fallback;

            return await reference.GetLocalizedStringAsync().ToUniTask();
        }
    }
#endif
}
