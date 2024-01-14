namespace Threadlink.Utilities.Editor
{
	using System.IO;
	using UnityEditor;
	using UnityEngine;
	using Utilities.Text;

	internal sealed class TextAssetCreationWizard : ScriptableWizard
	{
		[SerializeField] private string filePath = string.Empty;

		[Space(20)]

		[SerializeField] private string assetName = string.Empty;

		[MenuItem("Tools/Text Asset Wizard")]
		private static void CreateWizard()
		{
			DisplayWizard<TextAssetCreationWizard>("Create Text Asset");
		}

		private void OnWizardCreate()
		{
			if (filePath == null) return;

			//Create text asset in the specified path.
			string path = String.Construct(filePath, "/", assetName, ".txt");
			StreamWriter writer = new StreamWriter(path);

			writer.Write(string.Empty);
			writer.Close();

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private void OnWizardUpdate() { helpString = "Please set the path and name of the text file."; }
	}
}