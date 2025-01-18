namespace Threadlink.Editor
{
	using CSharpier;
	using System;
	using System.IO;
	using UnityEditor;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Core/Parcel Generator")]
	internal sealed class ThreadlinkStorageCodeGen : ScriptableObject
	{
		[Serializable]
		private sealed class ParcelConfig
		{
			[SerializeField] internal string parcelType = string.Empty;
			[SerializeField] internal string parcelNamespace = string.Empty;
		}

		[SerializeField] private string generatedParcelsDirectory = string.Empty;

		[Space(10)]

		[SerializeField] private ParcelConfig[] parcelsToGenerate = new ParcelConfig[0];

#if ODIN_INSPECTOR
		[Sirenix.OdinInspector.Button]
#else
        [ContextMenu("Generate Parcels")]
#endif
#pragma warning disable IDE0051
		private void GenerateParcels()
		{
			// Create the folder and get the project-relative path
			const string targetFolderName = "Generated Parcels";
			string projectRelativePath = generatedParcelsDirectory + "/" + targetFolderName;

			if (AssetDatabase.IsValidFolder(projectRelativePath) == false)
			{
				AssetDatabase.CreateFolder(generatedParcelsDirectory, targetFolderName);
			}

			// Convert the project-relative path to an absolute file system path
			string absolutePath = Path.Combine(Application.dataPath, projectRelativePath["Assets/".Length..]);

			int length = parcelsToGenerate.Length;

			for (int i = 0; i < length; i++)
			{
				var config = parcelsToGenerate[i];
				string parcelType = config.parcelType;

				string parcelName = $"{string.Join(string.Empty, char.ToUpper(parcelType[0]), parcelType[1..])}Parcel";

				string scriptContent = GenerateParcelScript(parcelName, parcelType, config.parcelNamespace);

				File.WriteAllText(Path.Combine(absolutePath, $"{parcelName}.cs"), CodeFormatter.Format(scriptContent).Code);
			}

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		private static string GenerateParcelScript(string parcelName, string parcelType, string parcelNamespace)
		{
			string usings = string.IsNullOrEmpty(parcelNamespace) ?
			string.Empty : string.Join(Environment.NewLine, $"using {parcelNamespace};");

			string scriptContent = $@"
			namespace Threadlink.Core.StorageAPI.Parcels
			{{
				{usings}

				public sealed class {parcelName} : ThreadlinkParcel<{parcelType}> {{ }}
			}}";

			return scriptContent;
		}
	}
}
