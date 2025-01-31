namespace Threadlink.Templates.AnimationUtilities
{
#if UNITY_EDITOR
	using UnityEditor;
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Animation Utilities/Animator Hash")]
	public sealed class AnimatorHash : ScriptableObject
	{
		public int Value => hashValue;

		[SerializeField] private string stringValue = string.Empty;

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private int hashValue = 0;

#if UNITY_EDITOR
#pragma warning disable IDE0051
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Generate Hash")]
#endif
		private void GenerateHash()
		{
			hashValue = Animator.StringToHash(stringValue);
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
#endif
	}
}