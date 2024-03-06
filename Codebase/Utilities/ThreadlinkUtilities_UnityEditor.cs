namespace Threadlink.Utilities.Editor
{
	using UnityEngine;

#if UNITY_EDITOR
	using UnityEditor;
	using UnityEditor.Build;
#endif

	public static class EditorUtilities
	{
#if UNITY_EDITOR
		public static bool EditorInOrWillChangeToPlaymode { get => EditorApplication.isPlayingOrWillChangePlaymode; }

		public static NamedBuildTarget CurrentNamedBuildTarget
		{
			get
			{
#if UNITY_SERVER
                    return NamedBuildTarget.Server;
#else
				BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
				BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
				NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
				return namedBuildTarget;
#endif
			}
		}

		/// <summary>
		/// Attempts to set a component in the OnValidate() or other Editor methods.
		/// </summary>
		public static void TrySetAttachedComponent<T>(this MonoBehaviour caller, ref T target) where T : Component
		{
			T component = caller.GetComponent<T>();

			if (target == null || target != component)
			{
				target = component;
				SetDirty(caller);
			}
		}

		/// <summary>
		/// Attempts to set an object in the OnValidate() or other Editor methods.
		/// </summary>
		public static void TrySetObject<T>(this Object caller, ref T target, T value) where T : Object
		{
			if (target == null && value != null)
			{
				target = value;
				SetDirty(caller);
			}
		}

		/// <summary>
		/// Attempts to set a value in the OnValidate() or other Editor methods.
		/// </summary>
		public static void TrySetValue<T>(this Object caller, ref T target, T value) where T : struct
		{
			target = value;
			SetDirty(caller);
		}

		/// <summary>
		/// Attempts to set a string in the OnValidate() or other Editor methods.
		/// </summary>
		public static void SetString(this Object caller, ref string target, string value)
		{
			if (string.IsNullOrEmpty(value) == false)
			{
				target = value;
				SetDirty(caller);
			}
		}

		/// <summary>
		/// Attempts to set a string in the OnValidate() or other Editor methods.
		/// </summary>
		public static void TrySetString(this Object caller, ref string target, string value)
		{
			if (string.IsNullOrEmpty(target) && string.IsNullOrEmpty(value) == false)
			{
				target = value;
				SetDirty(caller);
			}
		}

		public static void SetDirty(Object unityObject) { EditorUtility.SetDirty(unityObject); }

		public static AssetType LoadEditorAsset<AssetType>(string address) where AssetType : Object
		{
			AssetType asset = AssetDatabase.LoadAssetAtPath<AssetType>(address);

			if (asset == null) Debug.LogError("Could not find the editor asset requested.");

			return asset;
		}

		public static T[] FindAssetsOfType<T>() where T : Object
		{
			string assetType = typeof(T).Name;
			string[] guids = AssetDatabase.FindAssets($"t:{assetType}");
			int length = guids.Length;
			T[] assets = new T[length];

			for (int i = 0; i < length; i++)
			{
				assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
			}

			return assets;
		}

		public static void SaveAllAssets() { AssetDatabase.SaveAssets(); }
#endif
	}
}