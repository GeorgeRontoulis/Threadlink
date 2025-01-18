namespace Threadlink.Editor.Attributes
{
	using System.Linq;
	using UnityEditor;
	using UnityEditor.AddressableAssets;
	using UnityEngine;
	using System.IO;
	using Threadlink.Utilities.Collections;

	internal sealed class AddressableSelectorWindow : EditorWindow
	{
		private static SerializedProperty currentTargetProperty = null;
		private static string selectedGroup = string.Empty;
		private static string selectedAsset = string.Empty;
		private static System.Action<string> onSelectCallback = null;

		// Method to open the window and provide a callback for when an asset is selected
		public static void ShowWindow(ref SerializedProperty targetProperty, System.Action<string> onSelect)
		{
			currentTargetProperty = targetProperty;
			onSelectCallback = onSelect;
			var window = GetWindow<AddressableSelectorWindow>("Addressable Selector", true);
			window.Show();
			window.minSize = new(200, 128);
			window.maxSize = new(420, 200);
		}

		private void OnGUI()
		{
			if (onSelectCallback == null || currentTargetProperty == null)
			{
				EditorGUILayout.HelpBox("This Window requires the inspector that triggered it to be active in the Editor. " +
				"Additionally, crucial data is lost if a Domain Reload occurs while this window is active. " +
				"Please close and reopen the window if you had it open during a Domain Reload, and repeat your " +
				"configuration process.", MessageType.Warning);
			}
			else
			{
				GUILayout.Label("Select Addressable Group and Asset", EditorStyles.boldLabel);

				GUILayout.Space(15);

				var settings = AddressableAssetSettingsDefaultObject.Settings;

				if (settings == null)
				{
					EditorGUILayout.HelpBox("Addressable Asset Settings were not found!", MessageType.Warning);
					return;
				}

				var groupNames = settings.groups.Select(g => g.Name).ToArray();
				int groupIndex = Mathf.Max(0, System.Array.IndexOf(groupNames, selectedGroup));
				int newGroupIndex = EditorGUILayout.Popup("Addressable Group:", groupIndex, groupNames);

				if (newGroupIndex != groupIndex)
				{
					selectedGroup = groupNames[newGroupIndex];
					selectedAsset = currentTargetProperty.stringValue;
				}

				// Dropdown for selecting asset within the group
				var group = settings.groups.FirstOrDefault(g => g.Name == selectedGroup);

				if (group != null)
				{
					// Display only the asset name, but store the full address internally
					var assetEntries = group.entries.Select(e => new { e.address, assetName = Path.GetFileNameWithoutExtension(e.address) }).ToArray();
					var assetLabels = assetEntries.Select(e => e.assetName).ToArray();

					// Find the index of the currently selected asset
					int assetIndex = Mathf.Max(0, System.Array.IndexOf(assetEntries.Select(e => e.address).ToArray(), selectedAsset));
					int newAssetIndex = EditorGUILayout.Popup("Asset Name:", assetIndex, assetLabels);

					// Store the selected asset's full address, but display only its name
					if (newAssetIndex.IsWithinBoundsOf(assetEntries))
					{
						selectedAsset = assetEntries[newAssetIndex].address; // Use full address internally
					}
				}

				GUILayout.Space(15);

				if (GUILayout.Button("Apply", GUILayout.MinHeight(50), GUILayout.ExpandHeight(true)))
				{
					onSelectCallback?.Invoke(selectedAsset);
					Close();
				}
			}
		}
	}
}
