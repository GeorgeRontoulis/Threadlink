namespace Threadlink.Systems.Dextra
{
	using Sirenix.OdinInspector;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Dextra/Input Prompt Data")]
	internal sealed class DextraInputPromptData : ScriptableObject
	{
		[SerializeField] internal string promptText = null;

		[Space(10)]

		[PreviewField][SerializeField] internal Sprite mkbIcon = null;
		[PreviewField][SerializeField] internal Sprite xboxIcon = null;
		[PreviewField][SerializeField] internal Sprite dualsenseIcon = null;
	}
}
