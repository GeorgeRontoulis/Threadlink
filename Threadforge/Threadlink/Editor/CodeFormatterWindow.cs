namespace Threadlink.Editor
{
    using CSharpier;
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    internal sealed class CodeFormatterWindow : EditorWindow
    {
        [SerializeField] private DefaultAsset _folder;

        [MenuItem("Threadlink/Code Formatter")]
        private static void Open()
        {
            GetWindow<CodeFormatterWindow>("Code Formatter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Code Formatter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _folder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", _folder, typeof(DefaultAsset), false);

            using (new EditorGUI.DisabledScope(!IsValidFolder()))
            {
                if (GUILayout.Button("Format C# Files"))
                    FormatFolder();
            }
        }

        private bool IsValidFolder()
        {
            if (_folder == null)
                return false;

            var path = AssetDatabase.GetAssetPath(_folder);
            return AssetDatabase.IsValidFolder(path);
        }

        private void FormatFolder()
        {
            var assetPath = AssetDatabase.GetAssetPath(_folder);
            var absolutePath = Path.GetFullPath(assetPath);
            var files = Directory.EnumerateFiles(absolutePath, "*.cs", SearchOption.AllDirectories).ToArray();

            var formattedCount = 0;
            var failedCount = 0;

            try
            {
                for (var i = 0; i < files.Length; i++)
                {
                    var file = files[i];

                    EditorUtility.DisplayProgressBar("Formatting with CSharpier", Path.GetFileName(file), (float)i / files.Length);

                    try
                    {
                        var source = File.ReadAllText(file);
                        var result = CodeFormatter.Format(source);

                        if (result.CompilationErrors.Any())
                        {
                            failedCount++;

                            Debug.LogError($"CSharpier could not format '{file}':\n"
                            + string.Join(Environment.NewLine, result.CompilationErrors));

                            continue;
                        }

                        if (source == result.Code)
                            continue;

                        File.WriteAllText(file, result.Code);
                        formattedCount++;
                    }
                    catch (Exception exception)
                    {
                        failedCount++;
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"CSharpier finished. {formattedCount} file(s) changed, " +
            $"{failedCount} file(s) failed, {files.Length} file(s) scanned.");
        }
    }
}
