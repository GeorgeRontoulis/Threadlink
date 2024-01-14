namespace Threadlink.Utilities.Animation
{
	using UnityEngine;
	using Sirenix.OdinInspector;
	using Threadlink.Utilities.Editor;

	[CreateAssetMenu(menuName = "Threadlink/Animation Utilities/Animator Hash")]
	public sealed class AnimatorHash : ScriptableObject
	{
		public int Value { get => hashValue; }

		[SerializeField] private string stringValue = string.Empty;
		[ReadOnly][SerializeField] private int hashValue = 0;

#if UNITY_EDITOR
		[Button] private void GenerateHash() { this.TrySetValue(ref hashValue, Animator.StringToHash(stringValue)); }
#endif
	}
}