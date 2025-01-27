namespace Threadlink.Editor
{
	using UnityEditor;
	using UnityEngine;
	using Core;

	internal static class ThreadlinkPreferencesUtility
	{
		private static ThreadlinkPreferences _preferences;

		/// <summary>
		/// Retrieves the cached ThreadlinkPreferences asset or finds it if not cached.
		/// </summary>
		public static ThreadlinkPreferences Preferences
		{
			get
			{
				if (_preferences == null)
				{
					// Search for the asset using its type
					var guids = AssetDatabase.FindAssets("t:ThreadlinkPreferences");

					if (guids.Length > 0)
					{
						string path = AssetDatabase.GUIDToAssetPath(guids[0]);
						_preferences = AssetDatabase.LoadAssetAtPath<ThreadlinkPreferences>(path);
					}

					if (_preferences == null)
						Debug.LogWarning("ThreadlinkPreferences asset not found. Please create one via the Create Asset menu.");
				}

				return _preferences;
			}
		}
	}
}