namespace Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using Utilities.Strings;

    internal static class AssetGroupsCodeGen
    {
        private static readonly List<string> groupsBuffer = new(1);

        [MenuItem("Window/Asset Management/Addressables/Run Asset Groups CodeGen")]
        private static void GenerateAddressableGroupSignatures()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

            if (settings == null)
                throw new NullReferenceException("There is no Addressable Settings asset in the project!");

            var groups = settings.groups;
            int count = groups.Count;

            for (int i = 0; i < count; i++)
            {
                var groupName = groups[i].name;

                if (groupName.Equals("Built In Data") || groupName.Contains("Localization"))
                    continue;

                groupsBuffer.Add(groupName.Replace(" ", string.Empty).Replace("-", "_"));
            }

            string templateContent = editorConfig.AssetGroupsTemplate.text;
            string separator = string.Join(string.Empty, ",", Environment.NewLine);
            templateContent = templateContent.Replace("{DetectedGroups}", string.Join(separator, groupsBuffer));

            groupsBuffer.Clear();

            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(editorConfig.AssetGroupsTemplate).ToAbsolutePath());
            File.WriteAllText(Path.Combine(directory, "AssetGroups.cs"),
            CSharpier.CodeFormatter.Format(templateContent).Code);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }
    }

}
