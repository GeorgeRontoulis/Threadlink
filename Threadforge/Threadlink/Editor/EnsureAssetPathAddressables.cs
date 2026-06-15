namespace Threadlink.Editor
{
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEngine;

    internal static class EnsureAssetPathAddressables
    {
        /// <summary>
        /// Sets the address of every Addressable asset to its full project‑relative path.
        /// </summary>
        [MenuItem("Threadlink/Match Addressables to Paths")]
        private static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError("No Addressable Asset Settings found. Please create or assign one.");
                return;
            }

            bool anyChange = false;

            foreach (var group in settings.groups)
            {
                if (group == null || group.ReadOnly)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry == null
                    || string.IsNullOrEmpty(entry.AssetPath)
                    || entry.IsSubAsset
                    || entry.address.Equals(entry.AssetPath))
                    {
                        continue;
                    }

                    entry.SetAddress(entry.AssetPath);
                    anyChange = true;
                }
            }

            if (anyChange)
            {
                // Save the modified settings.
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("All Addressable addresses have been updated to their asset paths.");
            }
            else Debug.Log("All addresses already match their asset paths.");
        }
    }
}