namespace Threadlink.Editor
{
	using Attributes;
	using System;
	using System.IO;
	using UnityEditor;
	using UnityEngine;

	internal sealed class ScriptableObjectCreationWizard : ScriptableWizard
	{
		[ReadOnly, SerializeField] private TextAsset template = null;
		[SerializeField] private string assetMenuPath = string.Empty;
		[SerializeField] private string scriptPath = string.Empty;

		[Space(20)]

		[SerializeField] private string className = string.Empty;
		[SerializeField] private string baseType = "ScriptableObject";
		[SerializeField] private string classScope = string.Empty;
		[SerializeField] private string namespaceName = string.Empty;
		[SerializeField] private string[] necessaryUsings = new string[1] { "UnityEngine" };
		[SerializeField] private TextAsset implementationTemplate = null;

		[MenuItem("Threadlink/Scriptable Object Wizard")]
		private static void CreateWizard() => DisplayWizard<ScriptableObjectCreationWizard>("Create Scriptable Object Class");

		private void OnWizardCreate()
		{
			if (template == null) return;

			//Prepare the file:
			string templateContents = template.text;

			var generatedUsings = new string[necessaryUsings.Length];
			for (int i = 0; i < generatedUsings.Length; i++) generatedUsings[i] = $"using {necessaryUsings[i]};";

			//Modify the template copy:
			templateContents = templateContents.Replace("<Namespace>", namespaceName);
			templateContents = templateContents.Replace("<Usings>", string.Join(Environment.NewLine, generatedUsings));
			templateContents = templateContents.Replace("<AssetMenuPath>", assetMenuPath);
			templateContents = templateContents.Replace("<Scope>", classScope);
			templateContents = templateContents.Replace("<ClassName>", className);
			templateContents = templateContents.Replace("<BaseClass>", baseType);
			templateContents = templateContents.Replace("<Implementation>", implementationTemplate == null ? string.Empty : implementationTemplate.text);

			//Save to a new script in the specified path.
			var writer = new StreamWriter($"{scriptPath}/{className}.cs");

			writer.Write(templateContents);
			writer.Close();

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private void OnWizardUpdate() => helpString = "Please set the class name and path of the ScriptableObject.";
	}
}