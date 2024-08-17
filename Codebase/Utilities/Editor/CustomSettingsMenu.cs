/*#if UNITY_EDITOR
namespace Threadlink.Utilities.Editor.Preferences
{
	using System.Collections.Generic;
	using UnityEditor;
	using UnityEngine;

	[InitializeOnLoad]
	internal static class Handler
	{
		private const string CoreDeploymentKey = "CoreDeploymentSetting";

		static Handler()
		{
		}

		internal sealed class Settings
		{
			internal enum CoreDeploymentMethod { Automatic, Manual }

			internal CoreDeploymentMethod coreDeployment = 0;
		}

		internal static Settings GetEditorSettings()
		{
			return new Settings
			{
				coreDeployment = (Settings.CoreDeploymentMethod)EditorPrefs.GetInt(CoreDeploymentKey, 0),
			};
		}

		internal static void SetEditorSettings(Settings settings)
		{
			EditorPrefs.SetInt(CoreDeploymentKey, (int)settings.coreDeployment);
		}
	}

	internal static class SettingsGUIContent
	{
		private static readonly GUIContent coreDeploymentGUIContent = new("Core Deployment",
		"Select how to deploy Threadlink in the runtime. Automatic: Before the first scene loads. " +
		"Manual: Requires you to configure and add a dedicated Threadlink Scene to your build. " +
		"Use the premade ThreadlinkScene in the 'Required Scenes' folder of the package.");

		internal static void DrawSettingsButtons(Handler.Settings settings)
		{
			EditorGUILayout.Space(10);

			EditorGUI.indentLevel += 1;

			settings.coreDeployment = (Handler.Settings.CoreDeploymentMethod)EditorGUILayout.EnumPopup(coreDeploymentGUIContent,
			settings.coreDeployment, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(300));

			EditorGUI.indentLevel -= 1;
		}
	}

#if UNITY_2018_3_OR_NEWER
	internal static class ThreadlinkSettingsProvider
	{
		[SettingsProvider]
		public static SettingsProvider CreateSettingsProvider()
		{
			var provider = new SettingsProvider("Preferences/Threadlink", SettingsScope.User)
			{
				label = "Threadlink",

				guiHandler = (searchContext) =>
				{
					var settings = Handler.GetEditorSettings();

					EditorGUI.BeginChangeCheck();
					SettingsGUIContent.DrawSettingsButtons(settings);

					if (EditorGUI.EndChangeCheck()) Handler.SetEditorSettings(settings);
				},

				// Keywords for the search bar in the Unity Preferences menu
				keywords = new HashSet<string>(new[] { "Threadlink", "Settings" })

			};

			return provider;
		}
	}
#endif
}
#endif
*/