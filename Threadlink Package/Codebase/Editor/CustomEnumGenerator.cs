namespace Threadlink.Editor
{
	using System;
	using System.IO;
	using UnityEditor;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Threadlink.Editor.Attributes;
#endif

	[CreateAssetMenu(menuName = "Threadlink/CodeGen/Custom Enum Generator")]
	internal sealed class CustomEnumGenerator : ScriptableObject
	{
		private static readonly string entrySeparator = "," + Environment.NewLine;

		[SerializeField] private TextAsset definitionsFileTemplate = null;
		[SerializeField] private string definitionsFileName = string.Empty;
		[SerializeField] private DefaultAsset saveIn = null;

		[Space(10)]

		[SerializeField] private string[] customEnumDefinitions = new string[0];

#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Generate Custom Enum Definitions")]
#endif
#pragma warning disable IDE0051
		private void GenerateCustomEnumDefinitions()
		{
			var templateContent = definitionsFileTemplate.text.Replace("{CustomEntries}",
			string.Join(entrySeparator, customEnumDefinitions));

			File.WriteAllText(string.Join("/", AssetDatabase.GetAssetPath(saveIn),
			$"{definitionsFileName}.cs"), CSharpier.CodeFormatter.Format(templateContent).Code);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
	}
}
