namespace Threadlink.Utilities.Editor
{
#if UNITY_EDITOR
	using Attributes;
	using System.Linq;
	using Threadlink.Utilities.UnityLogging;
	using UnityEditor;
	using UnityEditor.AddressableAssets;
	using UnityEditor.AddressableAssets.Settings;
	using UnityEditor.AddressableAssets.Settings.GroupSchemas;
	using UnityEngine;
	using Utilities.Text;

	internal sealed class ThreadlinkSetupWizard : ScriptableWizard
	{
		private const string ThreadlinkGroup = "Threadlink Assets";

		[ReadOnly][SerializeField] private TextAsset scriptingSymbolsFile = null;
		[ReadOnly][SerializeField] private ThreadlinkEditorAddressablesContainer requiredAddressables = null;

		[Space(10)]

		[SerializeField] private string[] desiredScriptingSymbols = new string[0];

		[MenuItem("Threadlink/Initial Setup Wizard")]
		private static void CreateWizard()
		{
			DisplayWizard<ThreadlinkSetupWizard>("Initial Threadlink Setup", "Apply Configuration");
		}

		private void OnEnable()
		{
			helpString = "Please configure the Scripting Symbols Threadlink should use for your project.";

			if (scriptingSymbolsFile == null) return;

			if (desiredScriptingSymbols == null || desiredScriptingSymbols.Length <= 0)
				desiredScriptingSymbols = scriptingSymbolsFile.ToLineList().ToArray();
		}

		private void OnWizardCreate()
		{
			if (AddressableAssetSettingsDefaultObject.Settings != null)
			{
				var assets = requiredAddressables.adressableAssets;
				int length = assets.Length;

				for (int i = 0; i < length; i++)
					EnsureAssetIsAddressable(ThreadlinkGroup, AssetDatabase.GetAssetPath(assets[i]));
			}
			else UnityConsole.Notify(DebugNotificationType.Error, this,
			"No ''Addressable Asset Settings'' asset found! Make sure one is present in your project!");

			if (desiredScriptingSymbols != null && desiredScriptingSymbols.Length > 0)
			{
				var buildTarget = EditorUtilities.CurrentNamedBuildTarget;

				bool shouldUpdateSymbols = false;
				string existingSymbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

				int length = desiredScriptingSymbols.Length;

				for (int i = 0; i < length; i++)
				{
					if (existingSymbols.Contains(desiredScriptingSymbols[i]) == false)
					{
						shouldUpdateSymbols = true;
						break;
					}
				}

				if (shouldUpdateSymbols) PlayerSettings.SetScriptingDefineSymbols(buildTarget, desiredScriptingSymbols);
			}
			else UnityConsole.Notify(DebugNotificationType.Warning, this,
			"No Scripting Define Symbols provided! Project Symbols will remain unaffected!");

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}

		AddressableAssetGroup CreateGroup(string groupName)
		{
			// Get the AddressableAssetSettings which holds all the groups
			var settings = AddressableAssetSettingsDefaultObject.Settings;

			// Check if the group already exists
			AddressableAssetGroup group = null;

			try
			{
				group = settings.groups.First(g => g.Name == groupName);
			}
			catch (System.Exception) { }

			if (group == null)
			{
				group = settings.CreateGroup(groupName, false, true, false, null);
				settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, true, true);
			}

			if (group.HasSchema<BundledAssetGroupSchema>() == false) group.AddSchema<BundledAssetGroupSchema>();
			if (group.HasSchema<ContentUpdateGroupSchema>() == false) group.AddSchema<ContentUpdateGroupSchema>();

			var bundledAssetSchema = group.GetSchema<BundledAssetGroupSchema>();
			bundledAssetSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.Uncompressed;
			bundledAssetSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
			bundledAssetSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
			bundledAssetSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
			bundledAssetSchema.AssetLoadMode = UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode.AllPackedAssetsAndDependencies;

			settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, bundledAssetSchema, true, true);

			return group;
		}

		private void CreateOrModifyAddressableGroup(string groupName, string assetPath)
		{
			var group = CreateGroup(groupName);

			var guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (string.IsNullOrEmpty(guid) == false)
			{
				var settings = AddressableAssetSettingsDefaultObject.Settings;
				var entry = settings.CreateOrMoveEntry(guid, group);

				entry.SetAddress(assetPath);
				settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
			}
		}

		private void EnsureAssetIsAddressable(string groupName, string assetPath)
		{
			if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
			{
				UnityConsole.Notify(DebugNotificationType.Error, this, $"Asset does not exist: {assetPath}");
				return;
			}

			CreateOrModifyAddressableGroup(groupName, assetPath);
		}
	}
#endif
}