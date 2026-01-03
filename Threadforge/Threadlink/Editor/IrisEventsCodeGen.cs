namespace Threadlink.Editor.CodeGen
{
    using CSharpier;
    using Cysharp.Text;
    using Shared;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Strings;

    internal static class IrisEventsCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined Event IDs}";

        private static readonly HashSet<string> eventsBuffer = new(1);
        private static readonly HashSet<string> userDefinedLinesBuffer = new(1);

        [MenuItem("Threadlink/Run Iris Events CodeGen")]
        private static void RunIrisEventsCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            var nativeContent = editorConfig.NativeIrisEventsTemplate.text;
            var userTemplate = editorConfig.UserIrisEventsTemplate;
            string code = nativeContent.Replace(PLACEHOLDER, string.Empty);

            if (TryLoadUserEvents(userTemplate))
            {
                var userContent = ZString.Join(",", eventsBuffer.ToArray());
                var mergedContent = nativeContent.Replace(PLACEHOLDER, userContent);
                code = CodeFormatter.Format(mergedContent).Code;

                Debug.Log($"Applied {eventsBuffer.Count} User-Defined events!");
            }
            else Debug.LogWarning($"No User-Defined events detected in: {AssetDatabase.GetAssetPath(userTemplate)}");

            eventsBuffer.Clear();

            File.WriteAllText(AssetDatabase.GetAssetPath(editorConfig.IrisEventsScript).ToAbsolutePath(), code);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool TryLoadUserEvents(TextAsset userTemplate)
        {
            if (userTemplate == null) return false;

            static bool IsValidIdentifier(string name)
            {
                if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]))
                    return false;

                return name.All(c => char.IsLetterOrDigit(c) || c == '_');
            }

            userTemplate.ReadLines(userDefinedLinesBuffer);

            var events = userDefinedLinesBuffer
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("///"))
            .Where(line => !line.StartsWith("//"))
            .Where(IsValidIdentifier);

            foreach (var entry in events)
                eventsBuffer.Add(entry);

            userDefinedLinesBuffer.Clear();

            return eventsBuffer.Count > 0;
        }
    }
}