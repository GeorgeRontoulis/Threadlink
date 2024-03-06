#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

internal static class ThreadlinkUtilities_CustomPlaymode
{
	[MenuItem("Threadlink/Launch Testing Session")]
	public static void PlayFromAddressableInitScene()
	{
		if (EditorApplication.isPlaying == true)
		{
			EditorApplication.isPlaying = false;
			return;
		}

		EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
		EditorSceneManager.OpenScene("Assets/Threadlink/Required Scenes/InitializationScene.unity");
		EditorApplication.isPlaying = true;
	}
}
#endif