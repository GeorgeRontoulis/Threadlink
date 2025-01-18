namespace Threadlink.Editor.Utilities
{
	using UnityEditor;
	using UnityEditor.Build;
	using UnityEngine;

	public static class EditorUtilities
	{
		public static NamedBuildTarget CurrentNamedBuildTarget
		{
			get
			{
#if UNITY_SERVER
                    return NamedBuildTarget.Server;
#else
				var buildTarget = EditorUserBuildSettings.activeBuildTarget;
				var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
				var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

				return namedBuildTarget;
#endif
			}
		}

		public static T[] FindAssetsOfType<T>() where T : Object
		{
			string assetType = typeof(T).Name;
			var guids = AssetDatabase.FindAssets($"t:{assetType}");
			int length = guids.Length;
			var assets = new T[length];

			for (int i = 0; i < length; i++)
			{
				assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
			}

			return assets;
		}
	}
}