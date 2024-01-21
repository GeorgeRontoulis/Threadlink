namespace Threadlink.Utilities.Editor
{
	using CSharpier;
	using MassTransit;
	using Sirenix.OdinInspector;
	using System;
	using System.IO;
	using System.Text;
	using UnityEditor;
	using UnityEngine;
	using String = Text.String;

	internal sealed class ThreadlinkComponentEditor : ScriptableWizard
	{
		private static readonly StringBuilder StringBuilder = new();

		[ReadOnly][SerializeField] private TextAsset vaultTemplate = null;
		[ReadOnly][SerializeField] private UnityEngine.Object compilationTarget = null;

		[Space(10)]

		[SerializeField] private ThreadlinkComponentBlueprint[] componentBlueprints = new ThreadlinkComponentBlueprint[0];

		[MenuItem("Tools/Threadlink Component Editor")]
		private static void CreateWizard()
		{
			DisplayWizard<ThreadlinkComponentEditor>("Generate the Threadlink Component Vault code.", "Compile");
		}

		private void OnEnable()
		{
			componentBlueprints = EditorUtilities.FindAssetsOfType<ThreadlinkComponentBlueprint>();
		}

		private void OnWizardCreate()
		{
			string newLine = Environment.NewLine;
			string templateContents = vaultTemplate.text;

			if (componentBlueprints == null || componentBlueprints.Length <= 0) return;

			int length = componentBlueprints.Length;
			for (int i = 0; i < length; i++)
			{
				string componentCode = componentBlueprints[i].Compile();
				string conditionalNewLine = i == length - 1 ? string.Empty : newLine;

				StringBuilder.Append(String.Construct(componentCode, conditionalNewLine, conditionalNewLine));
			}

			templateContents = templateContents.Replace("<ComponentDefinitions>", StringBuilder.ToString());

			StringBuilder.Clear();

			for (int i = 0; i < length; i++)
			{
				string componentName = componentBlueprints[i].name;
				string propertyName = componentName + "s";

				StringBuilder.Append(String.Construct("public Dictionary<", typeof(NewId).Name, ",",
				componentName, "> ", propertyName, " { get; set; }", i == length - 1 ? string.Empty : newLine));
			}

			templateContents = templateContents.Replace("<ComponentCollections>", StringBuilder.ToString());
			templateContents = CodeFormatter.Format(templateContents).Code;

			StringBuilder.Clear();

			string path = String.Construct(Path.GetDirectoryName(AssetDatabase.GetAssetPath(compilationTarget)), "/", compilationTarget.name, ".cs");
			StreamWriter writer = new StreamWriter(path);

			writer.Write(templateContents);
			writer.Close();

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private void OnWizardUpdate()
		{
			helpString = "Please provide the desired Threadlink Components for compilation.";
		}
	}
}