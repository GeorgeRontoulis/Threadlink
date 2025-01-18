namespace Threadlink.Editor
{
	using Core;
	using System.Collections.Generic;
	using System.IO;
	using UnityEditor;
	using UnityEditor.AddressableAssets;
	using UnityEditor.AddressableAssets.Settings;
	using UnityEditor.AddressableAssets.Settings.GroupSchemas;
	using UnityEngine;

	internal static class ThreadlinkEditorPreferences
	{
		// Where we store the user ScriptableObject (created if missing).
		private const string USER_DATA_ASSET_PATH = "Assets/Threadlink/Threadlink User/Threadlink User Data.asset";

		// The JSON defaults path used only if we need to initialize a new asset.
		private const string DEFAULT_DATA_FILE_PATH = "Assets/Threadlink/Threadlink Package/ThreadlinkDefaults.json";

		// If there's no ScriptableObject yet, show "Initialize From Defaults".
		private static bool s_isFirstTimeUse = false;

		// Reference to the loaded or newly created ScriptableObject.
		private static ThreadlinkPreferences s_userDataAsset;

		// Editor-time caches for displaying ObjectFields (subsystems, additional assets).
		private static Object[] _nativeSubSystemsCache = new Object[0];
		private static Object[] _additionalNativeAssetsCache = new Object[0];

		[System.Serializable]
		private class ThreadlinkDefaultsData
		{
			public int coreDeployment;
			public string[] nativeSubSystems;
			public string[] additionalNativeAssets;
		}

		// -------------------------------------------------------------------
		// Static constructor: Check for asset and load or prompt for defaults
		// -------------------------------------------------------------------
		static ThreadlinkEditorPreferences()
		{
			LoadPreferences();
		}

		// -------------------------------------------------------------------
		// Load or create the ScriptableObject
		// -------------------------------------------------------------------
		private static void LoadPreferences()
		{
			s_userDataAsset = AssetDatabase.LoadAssetAtPath<ThreadlinkPreferences>(USER_DATA_ASSET_PATH);

			// If the .asset doesn't exist => first time use
			if (s_userDataAsset == null)
			{
				s_isFirstTimeUse = true;
				return; // We'll show a button to init from defaults
			}

			s_isFirstTimeUse = false;
			// Convert string[] -> Object[] for the editor UI
			_nativeSubSystemsCache = PathArrayToObjectArray(s_userDataAsset.nativeSubSystems);
			_additionalNativeAssetsCache = PathArrayToObjectArray(s_userDataAsset.additionalNativeAssets);

			// Ensure loaded objects get added to Addressables
			SyncWithAddressables(_nativeSubSystemsCache);
			SyncWithAddressables(_additionalNativeAssetsCache);
		}

		// -------------------------------------------------------------------
		// Initialize from JSON defaults => create new ScriptableObject
		// -------------------------------------------------------------------
		private static void InitializeFromDefaults()
		{
			// 1. Check if the user data asset already exists
			if (AssetDatabase.LoadAssetAtPath<ThreadlinkPreferences>(USER_DATA_ASSET_PATH) != null)
			{
				Debug.Log($"Threadlink: A user data asset already exists at {USER_DATA_ASSET_PATH}. Skipping defaults initialization.");
				return;
			}

			// 2. Check for the default JSON file
			if (!File.Exists(DEFAULT_DATA_FILE_PATH))
			{
				Debug.LogWarning($"Threadlink: No defaults file found at {DEFAULT_DATA_FILE_PATH}.");
				return;
			}

			// 3. Parse the JSON
			var defaultsJson = File.ReadAllText(DEFAULT_DATA_FILE_PATH);
			var data = JsonUtility.FromJson<ThreadlinkDefaultsData>(defaultsJson);
			if (data == null)
			{
				Debug.LogWarning($"Threadlink: Failed to parse defaults JSON at {DEFAULT_DATA_FILE_PATH}.");
				return;
			}

			// 4. Create a new SO
			s_userDataAsset = ScriptableObject.CreateInstance<ThreadlinkPreferences>();

			// 5. Apply defaults
			s_userDataAsset.coreDeployment = (ThreadlinkPreferences.CoreDeploymentMethod)data.coreDeployment;
			s_userDataAsset.nativeSubSystems = data.nativeSubSystems ?? new string[0];
			s_userDataAsset.additionalNativeAssets = data.additionalNativeAssets ?? new string[0];

			// 6. Ensure the folder structure exists
			var directory = Path.GetDirectoryName(USER_DATA_ASSET_PATH);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
				AssetDatabase.Refresh();
			}

			// 7. Save the new .asset
			AssetDatabase.CreateAsset(s_userDataAsset, USER_DATA_ASSET_PATH);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// 8. Finally, load them into caches
			LoadPreferences();
		}

		// -------------------------------------------------------------------
		// Save changes back into the ScriptableObject
		// -------------------------------------------------------------------
		private static void SavePreferences()
		{
			if (s_userDataAsset == null)
			{
				Debug.LogWarning("Threadlink: No user data asset. Initializing from defaults.");
				InitializeFromDefaults();
				return;
			}

			// Convert the caches -> string[] in the SO
			s_userDataAsset.nativeSubSystems = ObjectArrayToPathArray(_nativeSubSystemsCache);
			s_userDataAsset.additionalNativeAssets = ObjectArrayToPathArray(_additionalNativeAssetsCache);

			// Mark and save
			EditorUtility.SetDirty(s_userDataAsset);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// -------------------------------------------------------------------
		// Addressables Integration
		// -------------------------------------------------------------------
		private static void SyncWithAddressables(Object[] array)
		{
			foreach (var obj in array)
			{
				if (obj != null) AddToThreadlinkAssetsGroup(obj);
			}
		}

		private static void AddToThreadlinkAssetsGroup(Object obj)
		{
			if (!obj) return;

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
			if (settings == null) return;  // No Addressable settings found

			// Find or create "Threadlink Assets" group
			var group = settings.FindGroup("Threadlink Assets");
			if (group == null)
			{
				group = settings.CreateGroup("Threadlink Assets", false, true, false, new(), new System.Type[0]);
				settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, true, true);
			}

			if (!group.HasSchema<BundledAssetGroupSchema>())
				group.AddSchema<BundledAssetGroupSchema>();
			if (!group.HasSchema<ContentUpdateGroupSchema>())
				group.AddSchema<ContentUpdateGroupSchema>();

			var bundledAssetSchema = group.GetSchema<BundledAssetGroupSchema>();
			bundledAssetSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.Uncompressed;
			bundledAssetSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
			bundledAssetSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
			bundledAssetSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
			bundledAssetSchema.AssetLoadMode =
				UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode.AllPackedAssetsAndDependencies;

			settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, bundledAssetSchema, true, true);

			string assetPath = AssetDatabase.GetAssetPath(obj);
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (!string.IsNullOrEmpty(guid))
			{
				var entry = settings.CreateOrMoveEntry(guid, group);
				entry?.SetAddress(assetPath);
			}
		}

		private static void RemoveFromThreadlinkAssetsGroup(Object obj)
		{
			if (!obj) return;

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			if (settings == null) return;

			var group = settings.FindGroup("Threadlink Assets");
			if (group == null) return;

			string assetPath = AssetDatabase.GetAssetPath(obj);
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (!string.IsNullOrEmpty(guid))
			{
				settings.RemoveAssetEntry(guid);
			}
		}

		// -------------------------------------------------------------------
		// Converters for string[] <-> Object[]
		// -------------------------------------------------------------------
		private static string[] ObjectArrayToPathArray(Object[] objs)
		{
			if (objs == null) return new string[0];
			var paths = new List<string>();
			foreach (var obj in objs)
			{
				if (!obj)
				{
					paths.Add(string.Empty);
					continue;
				}
				var assetPath = AssetDatabase.GetAssetPath(obj);
				paths.Add(!string.IsNullOrEmpty(assetPath) ? assetPath : string.Empty);
			}
			return paths.ToArray();
		}

		private static Object[] PathArrayToObjectArray(string[] paths)
		{
			if (paths == null) return new Object[0];
			var objs = new List<Object>();
			foreach (var path in paths)
			{
				if (string.IsNullOrEmpty(path))
				{
					objs.Add(null);
					continue;
				}
				var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
				objs.Add(asset);
			}
			return objs.ToArray();
		}

		// -------------------------------------------------------------------
		// SettingsProvider (Preferences Window)
		// -------------------------------------------------------------------
		[SettingsProvider]
		public static SettingsProvider CreatePreferencesProvider()
		{
			return new SettingsProvider("Preferences/Threadlink", SettingsScope.User)
			{
				label = "Threadlink",

				guiHandler = (searchContext) =>
				{
					if (s_isFirstTimeUse)
					{
						EditorGUILayout.HelpBox("No Threadlink user data found (first time use).", MessageType.Info);
						if (GUILayout.Button("Initialize From Defaults"))
						{
							InitializeFromDefaults();
						}
						return;
					}

					// Show the Preferences
					if (s_userDataAsset == null)
					{
						EditorGUILayout.HelpBox(
							"User Data asset is missing or not loaded. This is unusual. Try resetting defaults.",
							MessageType.Warning);
						return;
					}

					// Core Deployment
					EditorGUILayout.LabelField("Core Settings", EditorStyles.boldLabel);
					var newCoreDeployment = (ThreadlinkPreferences.CoreDeploymentMethod)
						EditorGUILayout.EnumPopup("Core Deployment", s_userDataAsset.coreDeployment);
					if (newCoreDeployment != s_userDataAsset.coreDeployment)
					{
						s_userDataAsset.coreDeployment = newCoreDeployment;
						EditorUtility.SetDirty(s_userDataAsset);
						AssetDatabase.SaveAssets();
					}

					// Native Subsystems
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Native Subsystems", EditorStyles.boldLabel);
					DrawObjectArray(ref _nativeSubSystemsCache, "Subsystem");

					// Additional Native Assets
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Additional Native Assets", EditorStyles.boldLabel);
					DrawObjectArray(ref _additionalNativeAssetsCache, "Asset");

					// Optionally a "Reset Defaults" button
					EditorGUILayout.Space(15);
					if (GUILayout.Button("Reset Defaults"))
					{
						InitializeFromDefaults();
					}
				},

				keywords = new HashSet<string> { "Threadlink", "Deployment", "Subsystems", "NativeAssets" }
			};
		}

		// -------------------------------------------------------------------
		// Draw an Object array with add/remove UI
		// -------------------------------------------------------------------
		private static void DrawObjectArray(ref Object[] array, string labelPrefix)
		{
			if (array != null)
			{
				for (int i = 0; i < array.Length; i++)
				{
					var oldVal = array[i];

					EditorGUI.BeginChangeCheck();
					var newVal = EditorGUILayout.ObjectField(
						$"{labelPrefix} {i}",
						oldVal,
						typeof(Object),
						false
					);
					if (EditorGUI.EndChangeCheck())
					{
						// Remove old, add new
						if (oldVal != null) RemoveFromThreadlinkAssetsGroup(oldVal);
						if (newVal != null) AddToThreadlinkAssetsGroup(newVal);

						array[i] = newVal;
						SavePreferences();
					}
				}
			}

			EditorGUILayout.BeginHorizontal();

			if (GUILayout.Button($"Add {labelPrefix}"))
			{
				int oldSize = array?.Length ?? 0;
				System.Array.Resize(ref array, oldSize + 1);
				SavePreferences();
			}

			if ((array?.Length ?? 0) > 0 && GUILayout.Button($"Remove Last {labelPrefix}"))
			{
				var lastElement = array[array.Length - 1];
				if (lastElement != null) RemoveFromThreadlinkAssetsGroup(lastElement);

				System.Array.Resize(ref array, array.Length - 1);
				SavePreferences();
			}

			EditorGUILayout.EndHorizontal();
		}
	}
}
