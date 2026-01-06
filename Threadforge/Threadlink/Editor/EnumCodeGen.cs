namespace Threadlink.Editor
{
    using CSharpier;
    using Cysharp.Text;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Strings;

    internal static class EnumCodeGen
    {
        private static readonly HashSet<string> enumEntriesBuffer = new(1);
        private static readonly HashSet<string> userDefinedLinesBuffer = new(1);

        internal static bool TryGenerateEnum(TextAsset nativeTemplate, TextAsset userTemplate, MonoScript targetScript, string placeholder)
        {
            if (nativeTemplate == null || userTemplate == null | targetScript == null || string.IsNullOrEmpty(placeholder))
                return false;

            var nativeContent = nativeTemplate.text;
            string code = nativeContent.Replace(placeholder, string.Empty);

            if (TryLoadUserEnumEntries(userTemplate))
            {
                var userContent = ZString.Join(",", enumEntriesBuffer.ToArray());
                var mergedContent = nativeContent.Replace(placeholder, userContent);
                code = CodeFormatter.Format(mergedContent).Code;

                Debug.Log($"Applied {enumEntriesBuffer.Count} User-Defined Entries!");
            }
            else Debug.LogWarning($"No User-Defined Entries detected in: {AssetDatabase.GetAssetPath(userTemplate)}");

            enumEntriesBuffer.Clear();

            File.WriteAllText(AssetDatabase.GetAssetPath(targetScript).ToAbsolutePath(), code);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static bool TryLoadUserEnumEntries(TextAsset userTemplate)
        {
            if (userTemplate == null) return false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool IsValidIdentifier(string name)
            {
                if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]))
                    return false;

                return name.All(c => char.IsLetterOrDigit(c) || c == '_');
            }

            userTemplate.ReadLines(userDefinedLinesBuffer);

            var enumEntries = userDefinedLinesBuffer
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("///"))
            .Where(line => !line.StartsWith("//"))
            .Where(IsValidIdentifier);

            foreach (var entry in enumEntries)
                enumEntriesBuffer.Add(entry);

            userDefinedLinesBuffer.Clear();

            return enumEntriesBuffer.Count > 0;
        }
    }
}
