namespace Threadlink.Editor
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using CSharpier;
    using Cysharp.Text;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Strings;

    internal static class EnumCodeGen
    {
        private static readonly StringBuilder stringBuilder = new();

        private static readonly HashSet<string> EnumEntriesBuffer = new(1);
        private static readonly HashSet<string> LinesBuffer = new(1);

        internal static bool TryGenerateEnum(TextAsset nativeTemplate, TextAsset userTemplate, MonoScript targetScript, string placeholder)
        {
            if (nativeTemplate == null || userTemplate == null | targetScript == null || string.IsNullOrEmpty(placeholder))
                return false;

            var nativeContent = nativeTemplate.text;
            string code = nativeContent.Replace(placeholder, string.Empty);

            if (TryLoadEnumEntries(userTemplate))
            {
                var mergedContent = nativeContent.Replace(placeholder, ZString.Join(",", EnumEntriesBuffer.ToArray()));
                code = CodeFormatter.Format(mergedContent).Code;

                Debug.Log($"Applied {EnumEntriesBuffer.Count} User-Defined Entries!");
            }
            else Debug.LogWarning($"No User-Defined Entries detected in: {AssetDatabase.GetAssetPath(userTemplate)}");

            EnumEntriesBuffer.Clear();

            File.WriteAllText(AssetDatabase.GetAssetPath(targetScript).ToAbsolutePath(), code);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        internal static bool TryGenerateEnum<T>(TextAsset template, ReadOnlySpan<T> sourceArrayView,
        Func<T, string> entryExtractionMethod, MonoScript targetScript, string placeholder)
        {
            if (targetScript == null || string.IsNullOrEmpty(placeholder))
                return false;

            var templateContent = template.text;
            string code = templateContent.Replace(placeholder, string.Empty);

            if (TryLoadEnumEntries(sourceArrayView, entryExtractionMethod))
            {
                var generatedContent = templateContent.Replace(placeholder, ZString.Join(",", EnumEntriesBuffer.ToArray()));
                code = CodeFormatter.Format(generatedContent).Code;

                Debug.Log($"Applied {EnumEntriesBuffer.Count} User-Defined Entries!");
            }
            else Debug.LogWarning($"No User-Defined Entries detected in: {AssetDatabase.GetAssetPath(template)}");

            EnumEntriesBuffer.Clear();

            File.WriteAllText(AssetDatabase.GetAssetPath(targetScript).ToAbsolutePath(), code);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static bool TryLoadEnumEntries(TextAsset template)
        {
            if (template == null) return false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool IsValidIdentifier(string name)
            {
                if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]))
                    return false;

                return name.All(c => char.IsLetterOrDigit(c) || c == '_');
            }

            template.ReadLines(LinesBuffer);

            var enumEntries = LinesBuffer
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("///"))
            .Where(line => !line.StartsWith("//"))
            .Where(IsValidIdentifier);

            foreach (var entry in enumEntries)
                EnumEntriesBuffer.Add(entry);

            LinesBuffer.Clear();

            return EnumEntriesBuffer.Count > 0;
        }

        private static bool TryLoadEnumEntries<T>(ReadOnlySpan<T> arrayView, Func<T, string> entryExtractionMethod)
        {
            if (arrayView.IsEmpty || entryExtractionMethod == null)
                return false;

            int length = arrayView.Length;

            for (int i = 0; i < length; i++)
            {
                var entry = entryExtractionMethod(arrayView[i]);

                if (string.IsNullOrWhiteSpace(entry) || string.IsNullOrEmpty(entry))
                    Scribe.Send<Threadlink>("Invalid Entry detected during CodeGen!").ToUnityConsole(DebugType.Warning);
                else
                    EnumEntriesBuffer.Add(SanitizeEnumName(entry));

            }

            LinesBuffer.Clear();

            return EnumEntriesBuffer.Count > 0;
        }

        public static string SanitizeEnumName(string name)
        {
            // First character: must be letter or underscore
            char c = name[0];

            stringBuilder.Clear();
            stringBuilder.Append(IsIdentifierStart(c) ? c : '_');

            // Remaining characters: letter, digit, or underscore
            int length = name.Length;
            for (int i = 1; i < length; i++)
            {
                c = name[i];
                stringBuilder.Append(IsIdentifierPart(c) ? c : '_');
            }

            var output = stringBuilder.ToString();
            stringBuilder.Clear();
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
