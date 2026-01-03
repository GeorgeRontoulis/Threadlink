#if UNITY_EDITOR
namespace Threadlink.Shared
{
    using Core.NativeSubsystems.Scribe;
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public static class ThreadlinkConfigFinder
    {
        private const string ERROR_MSG = "User Config not found. Please create one via the Create Asset menu.";
        private static readonly Dictionary<Type, ScriptableObject> CachedConfigs = new(1);

        public static bool TryGetConfig<T>(out T result) where T : ScriptableObject
        {
            var requestedType = typeof(T);

            if (CachedConfigs.TryGetValue(requestedType, out var scriptableObject))
            {
                result = scriptableObject as T;
                return true;
            }
            else
            {
                var guids = AssetDatabase.FindAssets($"t:{requestedType.Name}");

                if (guids.Length > 0)
                {
                    result = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    CachedConfigs.Add(requestedType, result);
                    return true;
                }
                else
                {
                    Scribe.Send<T>(ERROR_MSG).ToUnityConsole(DebugType.Error);
                    result = null;
                    return false;
                }
            }
        }
    }
}
#endif