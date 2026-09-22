namespace Threadlink.Editor.Sentinel
{
    using Core.NativeSubsystems.Sentinel;
    using System;
    using System.Collections.Generic;
    using UnityEditor;

    [CustomEditor(typeof(SentinelConfig))]
    internal sealed class SentinelConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var target = EditorUserBuildSettings.activeBuildTarget;
            var platform = SentinelBuildPlatformResolver.Resolve(target);

            EditorGUILayout.LabelField("Current Build Target", target.ToString());
            EditorGUILayout.LabelField("Sentinel Platform", platform.ToString());
            EditorGUILayout.Space(4f);

            if (platform is SentinelPlatformMarker.Unknown)
            {
                EditorGUILayout.HelpBox("Sentinel does not currently support this Unity build target.", MessageType.Error);

                serializedObject.ApplyModifiedProperties();
                return;
            }

            var allowed = SentinelDistributionPolicy.GetAllowed(platform);

            if (allowed.Length == 0)
            {
                EditorGUILayout.HelpBox($"No Sentinel distributions are defined for {platform}.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (allowed.Length == 1)
            {
                EditorGUILayout.LabelField("Distribution", GetFriendlyName(allowed[0]));

                EditorGUILayout.HelpBox
                (
                    "This target resolves its Sentinel distribution automatically. " +
                    "The serialized distribution choice is preserved but ignored for this target.",
                    MessageType.Info
                );

                serializedObject.ApplyModifiedProperties();
                return;
            }

            var property = serializedObject.FindProperty(SentinelConfig.EditorOnly_DistributionPropertyName);

            if (property == null)
            {
                EditorGUILayout.HelpBox("Could not locate SentinelConfig's managed distribution reference.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }


            if (property.managedReferenceValue is SentinelDistributionConfiguration assigned && !assigned.Supports(platform))
            {
                EditorGUILayout.HelpBox
                (
                    $"{GetFriendlyName(assigned.Distribution)} is not valid for {platform}. " +
                    "Select one of the distributions available for the current target.",
                    MessageType.Warning
                );
            }

            var filters = new List<Func<Type, bool>>(1)
            {
                type =>
                {
                    if (!typeof(SentinelDistributionConfiguration).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        return false;
                    }

                    try
                    {
                        return Activator.CreateInstance(type) is SentinelDistributionConfiguration candidate
                        && candidate.Supports(platform);
                    }
                    catch
                    {
                        return false;
                    }
                }
            };

            EditorGUILayout.LabelField("Distribution");

            var buttonRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            // This is the same managed-reference selection UI used by Threadlink's
            // SerializeReferenceButtonAttribute drawer, with a target-aware filter added.
            property.DrawSelectionButtonForManagedReference(buttonRect, filters);

            EditorGUILayout.Space(4f);

            EditorGUILayout.HelpBox
            (
                "The selector only exposes distributions valid for the active Unity target. " +
                "Switching target changes the available managed-reference types automatically.",
                MessageType.None
            );

            serializedObject.ApplyModifiedProperties();
        }

        private static string GetFriendlyName(SentinelDistribution distribution)
        {
            return distribution switch
            {
                SentinelDistribution.MicrosoftStore => "Microsoft Store",
                SentinelDistribution.MacAppStore => "Mac App Store",
                SentinelDistribution.AppleAppStore => "Apple App Store",
                SentinelDistribution.GooglePlay => "Google Play",
                SentinelDistribution.AmazonAppstore => "Amazon Appstore",
                _ => distribution.ToString()
            };
        }
    }
}
