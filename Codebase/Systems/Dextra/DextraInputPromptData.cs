namespace Threadlink.Systems.Dextra
{
	using Threadlink.Utilities.Editor.Attributes;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Dextra/Input Prompt Data")]
	internal sealed class DextraInputPromptData : ScriptableObject
	{
		[SerializeField] internal string promptText = null;

		[Space(10)]

		[SpritePreview][SerializeField] internal Sprite mkbIcon = null;
		[SpritePreview][SerializeField] internal Sprite xboxIcon = null;
		[SpritePreview][SerializeField] internal Sprite dualsenseIcon = null;
	}
}
