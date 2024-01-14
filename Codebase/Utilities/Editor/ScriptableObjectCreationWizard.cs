namespace Threadlink.Utilities.Editor
{
	using System.IO;
	using UnityEditor;
	using UnityEngine;
	using Utilities.Text;

	internal sealed class ScriptableObjectCreationWizard : ScriptableWizard
	{
		[SerializeField] private DynamicEditorAssetPath templatePath = null;
		[SerializeField] private string assetMenuPath = string.Empty;
		[SerializeField] private string scriptPath = string.Empty;

		[Space(20)]

		[SerializeField] private string className = string.Empty;
		[SerializeField] private string baseType = "ScriptableObject";
		[SerializeField] private string classScope = string.Empty;
		[SerializeField] private string namespaceName = string.Empty;

		[MenuItem("Tools/Scriptable Object Wizard")]
		private static void CreateWizard()
		{
			DisplayWizard<ScriptableObjectCreationWizard>("Create Scriptable Object Class");
		}

		private void OnWizardCreate()
		{
			if (templatePath == null) return;

			//Prepare the file:
			StreamReader reader = new StreamReader(templatePath.AbsolutePath);
			string templateContents = reader.ReadToEnd();

			reader.Close();

			//Modify the template copy:
			templateContents = templateContents.Replace("<Namespace>", namespaceName);
			templateContents = templateContents.Replace("<AssetMenuPath>", assetMenuPath);
			templateContents = templateContents.Replace("<Scope>", classScope);
			templateContents = templateContents.Replace("<ClassName>", className);
			templateContents = templateContents.Replace("<BaseClass>", baseType);

			//Save to a new script in the specified path.
			string path = String.Construct(scriptPath, "/", className, ".cs");
			StreamWriter writer = new StreamWriter(path);

			writer.Write(templateContents);
			writer.Close();

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private void OnWizardUpdate()
		{
			helpString = "Please set the class name and path of the ScriptableObject class.";
		}
	}
}