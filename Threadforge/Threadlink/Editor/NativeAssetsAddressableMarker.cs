namespace Threadlink.Editor
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;

    internal static class NativeAssetsAddressableMarker
    {
        [MenuItem("Threadlink/Mark Native Assets as Addressable")]
        private static void MarkNativeAssetsAsAddressable()
        {
            if (ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkNativeConfig config))
            {
                const string GROUP = "Threadlink Assets";

                var guids = config.NativeAssetGUIDs;
                int length = guids.Length;

                for (int i = 0; i < length; i++)
                    MarkAddressable(guids[i], GROUP);

                MarkAddressable(AssetDatabase.AssetPathToGUID(NativeConstants.Addressables.NATIVE_CONFIG), GROUP);
            }
            else Scribe.Send<ThreadlinkNativeConfig>("Could not find config! Create one and execute the operation again!").ToUnityConsole(DebugType.Error);
        }

        private static void MarkAddressable(string assetGUID, string groupName = null, string label = null)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Scribe.Send<Threadlink>("Addressables settings not found!").ToUnityConsole(DebugType.Error);
                return;
            }

            if (string.IsNullOrEmpty(assetGUID))
            {
                Scribe.Send<Threadlink>("Invalid GUID detected. Will not mark native asset as addressable!").ToUnityConsole(DebugType.Error);
                return;
            }

            AddressableAssetGroup group;

            if (string.IsNullOrEmpty(groupName))
                group = settings.DefaultGroup;
            else
            {
                var foundGroup = settings.FindGroup(groupName);

                if (foundGroup == null)
                {
                    Scribe.Send<Threadlink>("The requested Asset Group was not found! Will use the Default Group instead.").ToUnityConsole(DebugType.Warning);
                    group = settings.DefaultGroup;
                }
                else group = foundGroup;
            }

            var entry = settings.FindAssetEntry(assetGUID);
            AddressableAssetSettings.ModificationEvent modEvent;

            if (entry != null)
            {
                if (entry.parentGroup.Equals(group))
                    return;
                else
                {
                    settings.MoveEntry(entry, group);
                    modEvent = AddressableAssetSettings.ModificationEvent.EntryMoved;
                }
            }
            else
            {
                entry = settings.CreateOrMoveEntry(assetGUID, group);
                modEvent = AddressableAssetSettings.ModificationEvent.EntryCreated;
            }

            if (!string.IsNullOrEmpty(label))
                entry.SetLabel(label, true);

            entry.address = AssetDatabase.GUIDToAssetPath(assetGUID);
            settings.SetDirty(modEvent, entry, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
