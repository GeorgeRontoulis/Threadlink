namespace Threadlink.Utilities.Editor.Attributes
{
#if UNITY_EDITOR
	using System.Linq;
	using UnityEditor;
	using UnityEditor.AddressableAssets;
	using UnityEngine;
	using Utilities.Collections;

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
			window.minSize = new Vector2(200, 128);
			window.maxSize = new Vector2(420, 200);
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
					var assetLabels = group.entries.Select(e => e.address).ToArray();
					int assetIndex = Mathf.Max(0, System.Array.IndexOf(assetLabels, selectedAsset));
					int newAssetIndex = EditorGUILayout.Popup("Asset Address:", assetIndex, assetLabels);

					if (newAssetIndex.IsWithinBoundsOf(assetLabels)) selectedAsset = assetLabels[newAssetIndex];
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
#endif
}