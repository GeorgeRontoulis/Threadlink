namespace Threadlink.Editor
{
	using System.IO;
	using UnityEditor;
	using UnityEngine;

	internal sealed class TextAssetCreationWizard : ScriptableWizard
	{
		[SerializeField] private string filePath = string.Empty;

		[Space(20)]

		[SerializeField] private string assetName = string.Empty;

		[MenuItem("Threadlink/Text Asset Wizard")]
		private static void CreateWizard() => DisplayWizard<TextAssetCreationWizard>("Create Text Asset");

		private void OnWizardCreate()
		{
			if (filePath == null) return;

			var writer = new StreamWriter($"{filePath}/{assetName}.txt");

			writer.Write(string.Empty);
			writer.Close();

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private void OnWizardUpdate() => helpString = "Please set the path and name of the text file.";
	}
}