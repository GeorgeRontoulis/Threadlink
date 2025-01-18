namespace Threadlink.Editor
{
	using System.IO;
	using UnityEditor;
	using UnityEditor.SceneManagement;
	using UnityEngine;

	internal sealed class SceneBrowserWindow : EditorWindow
	{
		private string directoryPath = "Assets/Threadlink/Threadlink User/Scenes"; // Default directory path
		private Vector2 scrollPos;

		[MenuItem("Threadlink/Scene Browser")]
		public static void ShowWindow() => GetWindow<SceneBrowserWindow>("Scene Browser");

		private void OnGUI()
		{
			GUILayout.Label("Scene Browser", EditorStyles.boldLabel);

			directoryPath = EditorGUILayout.TextField("Directory Path:", directoryPath);

			if (GUILayout.Button("Refresh")) Repaint();

			GUILayout.Space(10);

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			var paths = Directory.GetFiles(directoryPath, "*.unity");

			foreach (string scenePath in paths)
			{
				if (GUILayout.Button(Path.GetFileNameWithoutExtension(scenePath))) OpenScene(scenePath);
			}

			EditorGUILayout.EndScrollView();
		}

		private void OpenScene(string scenePath)
		{
			if (EditorApplication.isPlaying)
			{
				Debug.LogWarning("Cannot open scene while in Play mode.", this);
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				EditorSceneManager.OpenScene(scenePath);
		}
	}
}