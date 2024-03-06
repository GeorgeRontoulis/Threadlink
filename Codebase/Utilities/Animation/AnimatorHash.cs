namespace Threadlink.Utilities.Animation
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
using Threadlink.Utilities.Editor.Attributes;
#endif
	using Threadlink.Utilities.Editor;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Animation Utilities/Animator Hash")]
	public sealed class AnimatorHash : ScriptableObject
	{
		public int Value { get => hashValue; }

		[SerializeField] private string stringValue = string.Empty;

#if ODIN_INSPECTOR
		[ReadOnly]
#elif THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private int hashValue = 0;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Generate Hash")]
#endif
		private void GenerateHash() { this.TrySetValue(ref hashValue, Animator.StringToHash(stringValue)); }
#endif
	}
}