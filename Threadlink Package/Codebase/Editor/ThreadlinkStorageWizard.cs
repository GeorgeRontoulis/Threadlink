namespace Threadlink.Editor
{
	using Core.StorageAPI;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using UnityEditor;
	using UnityEngine;

	internal sealed class ThreadlinkStorageWizard : EditorWindow
	{
		/// <summary>
		/// Holds both the parcel type and a custom name the user wants to give it.
		/// Also includes the allowCloning toggle.
		/// </summary>
		private sealed class ParcelCreationEntry
		{
			internal Type ParcelType { get; set; }
			internal string CustomName { get; set; }
			internal bool AllowCloning { get; set; }
		}

		// Name of the new storage asset
		private string _newStorageName = "NewThreadlinkStorage";

		// All possible parcel types found in "Threadlink.User" assembly
		private readonly List<Type> _parcelTypes = new();

		// Index in the popup for selecting the next parcel type
		private int _selectedParcelTypeIndex;

		// The custom name for the next parcel
		private string _nextParcelName = "NewParcel";

		// Whether the next parcel should allow cloning
		private bool _nextAllowCloning = false;

		// Our "to-add" list, holding type + custom name + allowCloning for each entry
		private readonly List<ParcelCreationEntry> _parcelsToAdd = new();

		[MenuItem("Threadlink/Storage Wizard")]
		private static void ShowWindow()
		{
			GetWindow<ThreadlinkStorageWizard>("Threadlink Storage Wizard");
		}

		private void OnEnable()
		{
			RefreshParcelTypes();
		}

		private void OnGUI()
		{
			GUILayout.Label("Create a New Threadlink Storage", EditorStyles.boldLabel);

			EditorGUILayout.Space(10);

			// 1) Storage Name
			_newStorageName = EditorGUILayout.TextField("Storage Name:", _newStorageName);

			EditorGUILayout.Space(10);

			// 2) Parcel Types to Add
			DrawParcelTypePicker();

			EditorGUILayout.Space(15);

			// 3) Create Button
			if (GUILayout.Button("Create Storage", GUILayout.Height(35)))
			{
				CreateThreadlinkStorageWithParcels();
			}
		}

		private void DrawParcelTypePicker()
		{
			if (_parcelTypes.Count == 0)
			{
				EditorGUILayout.HelpBox(
					"No non-abstract subclasses of ThreadlinkParcel found in 'Threadlink.User' assembly.",
					MessageType.Warning
				);
				return;
			}

			EditorGUILayout.LabelField("Add Parcels to Create:", EditorStyles.boldLabel);
			EditorGUILayout.Space(10);

			// -- Next Parcel Selection --
			// 1) Parcel type popup
			_selectedParcelTypeIndex = EditorGUILayout.Popup(
				"Parcel Type:",
				_selectedParcelTypeIndex,
				_parcelTypes.Select(t => t.Name).ToArray()
			);

			// 2) Custom parcel name
			_nextParcelName = EditorGUILayout.TextField("Parcel Name:", _nextParcelName);

			// 3) Allow cloning toggle
			_nextAllowCloning = EditorGUILayout.Toggle("Allow Cloning?", _nextAllowCloning);

			EditorGUILayout.Space(15);

			// Add button
			if (GUILayout.Button("Add to List", GUILayout.Height(35)))
			{
				var chosenType = _parcelTypes[_selectedParcelTypeIndex];
				var entry = new ParcelCreationEntry
				{
					ParcelType = chosenType,
					CustomName = string.IsNullOrWhiteSpace(_nextParcelName)
						? chosenType.Name
						: _nextParcelName,
					AllowCloning = _nextAllowCloning
				};

				_parcelsToAdd.Add(entry);

				// Reset the fields for convenience
				_nextParcelName = "NewParcel";
				_nextAllowCloning = false;
			}

			EditorGUILayout.Space(10);

			// -- Already planned parcels --
			if (_parcelsToAdd.Count == 0)
			{
				EditorGUILayout.HelpBox("No parcels selected.", MessageType.None);
			}
			else
			{
				EditorGUILayout.LabelField("Parcels Pending Creation:", EditorStyles.boldLabel);

				for (int i = 0; i < _parcelsToAdd.Count; i++)
				{
					var entry = _parcelsToAdd[i];

					EditorGUILayout.BeginVertical("box");
					EditorGUILayout.LabelField($"Parcel Type: {entry.ParcelType.Name}", EditorStyles.boldLabel);

					// Custom name
					entry.CustomName = EditorGUILayout.TextField("Name:", entry.CustomName);

					// Allow Cloning
					entry.AllowCloning = EditorGUILayout.Toggle("Allow Cloning?", entry.AllowCloning);

					// Remove button
					if (GUILayout.Button("Remove", GUILayout.Width(60)))
					{
						_parcelsToAdd.RemoveAt(i);
						i--;
					}
					EditorGUILayout.EndVertical();

					EditorGUILayout.Space(5);
				}
			}
		}

		private void CreateThreadlinkStorageWithParcels()
		{
			// Prompt user for save location
			string path = EditorUtility.SaveFilePanelInProject(
				"Save Threadlink Storage",
				_newStorageName + ".asset",
				"asset",
				"Choose where to save the new ThreadlinkStorage."
			);

			if (string.IsNullOrEmpty(path)) return; // User cancelled

			// Create the storage asset
			var storage = CreateInstance<ThreadlinkStorage>();
			AssetDatabase.CreateAsset(storage, path);

			// For each staged parcel, create a sub-asset, set custom name/allowCloning, and add to storage
			foreach (var entry in _parcelsToAdd)
			{
				var parcel = CreateInstance(entry.ParcelType) as ThreadlinkParcel;
				if (parcel != null)
				{
					parcel.name = entry.CustomName;
					parcel.allowCloning = entry.AllowCloning;

					// Attach as sub-asset
					AssetDatabase.AddObjectToAsset(parcel, storage);

					// Also add to the storage’s parcel list
					storage.Parcels.Add(parcel);
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// Clear the staged list
			_parcelsToAdd.Clear();

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = storage;
		}

		private void RefreshParcelTypes()
		{
			_parcelTypes.Clear();

			// Get only the 'Threadlink.User' assembly
			Assembly userAssembly = AppDomain.CurrentDomain
				.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name == "Threadlink.User");

			if (userAssembly == null)
			{
				Debug.LogWarning("Could not find assembly 'Threadlink.User'.");
				return;
			}

			// Collect all non-abstract subclasses of ThreadlinkParcel in 'Threadlink.User'
			var typesInUserAssembly = userAssembly.GetTypes();
			foreach (var t in typesInUserAssembly)
			{
				if (!t.IsAbstract && t.IsSubclassOf(typeof(ThreadlinkParcel)))
				{
					_parcelTypes.Add(t);
				}
			}
		}
	}
}
