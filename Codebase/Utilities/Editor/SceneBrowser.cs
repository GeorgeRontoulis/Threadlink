namespace Threadlink.Utilities.Editor
{
	using System.IO;
	using UnityEditor;
	using UnityEditor.SceneManagement;
	using UnityEngine;

	internal sealed class SceneBrowserWindow : EditorWindow
	{
		private string directoryPath = "Assets/Scenes"; // Default directory path
		private Vector2 scrollPos;

		[MenuItem("Threadlink/Scene Browser")]
		public static void ShowWindow()
		{
			GetWindow<SceneBrowserWindow>("Scene Browser");
		}

		private void OnGUI()
		{
			GUILayout.Label("Scene Browser", EditorStyles.boldLabel);

			directoryPath = EditorGUILayout.TextField("Directory Path:", directoryPath);

			if (GUILayout.Button("Refresh"))
			{
				RefreshSceneList();
			}

			GUILayout.Space(10);

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			foreach (string scenePath in Directory.GetFiles(directoryPath, "*.unity"))
			{
				if (GUILayout.Button(Path.GetFileNameWithoutExtension(scenePath)))
				{
					OpenScene(scenePath);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void RefreshSceneList()
		{
			Repaint();
		}

		private void OpenScene(string scenePath)
		{
			if (EditorApplication.isPlaying)
			{
				UnityLogging.UnityConsole.Notify(UnityLogging.DebugNotificationType.Warning,
				this, "Cannot open scene while in Play mode.");
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				EditorSceneManager.OpenScene(scenePath);
			}
		}
	}
}