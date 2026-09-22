namespace Threadlink.Editor.Sentinel
{
    using Core;
    using Core.NativeSubsystems.Sentinel;
    using Generated;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using NativeResources = Generated.ThreadlinkIDs.Addressables.NativeResources;

    internal static class SentinelEditorConfigResolver
    {
        internal static bool TryGetSentinelConfig(
            out SentinelConfig config,
            out string error)
        {
            var guids = AssetDatabase.FindAssets("t:SentinelConfig");

            if (guids.Length == 0)
            {
                config = null;
                error = "No SentinelConfig asset exists.";
                return false;
            }

            if (guids.Length > 1)
            {
                config = null;
                error = $"Expected exactly one SentinelConfig asset, found {guids.Length}.";
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<SentinelConfig>(path);

            if (config == null)
            {
                error = $"Could not load SentinelConfig at '{path}'.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool ValidateNativeConfigReference(
            SentinelConfig sentinelConfig,
            out string error)
        {
            var nativeGuids = AssetDatabase.FindAssets("t:ThreadlinkNativeConfig");

            if (nativeGuids.Length == 0)
            {
                error = "ThreadlinkNativeConfig asset was not found.";
                return false;
            }

            if (nativeGuids.Length > 1)
            {
                error = $"Expected exactly one ThreadlinkNativeConfig asset, found {nativeGuids.Length}.";
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(nativeGuids[0]);
            var nativeConfig = AssetDatabase.LoadAssetAtPath<ThreadlinkNativeConfig>(path);

            if (nativeConfig == null)
            {
                error = $"Could not load ThreadlinkNativeConfig at '{path}'.";
                return false;
            }

            if (!nativeConfig.EditorOnly_TryGetNativeResourceGUID(
                NativeResources.SentinelConfig,
                out string configuredGuid))
            {
                error = "ThreadlinkNativeConfig has no SentinelConfig Native Resource mapping.";
                return false;
            }

            string sentinelPath = AssetDatabase.GetAssetPath(sentinelConfig);
            string expectedGuid = AssetDatabase.AssetPathToGUID(sentinelPath);

            if (configuredGuid != expectedGuid)
            {
                error =
                    "ThreadlinkNativeConfig's SentinelConfig Native Resource does not reference " +
                    $"the canonical SentinelConfig asset at '{sentinelPath}'.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
