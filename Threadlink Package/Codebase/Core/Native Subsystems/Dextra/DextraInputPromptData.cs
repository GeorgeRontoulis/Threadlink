namespace Threadlink.Core.Subsystems.Dextra
{
#if UNITY_EDITOR && THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif

	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Dextra/Input Prompt Data")]
	internal sealed class DextraInputPromptData : ScriptableObject
	{
		[SerializeField] internal string promptText = null;

		[Space(10)]

#if UNITY_EDITOR && THREADLINK_INSPECTOR
		[SpritePreview]
#endif
		[SerializeField] internal Sprite mkbIcon = null;

#if UNITY_EDITOR && THREADLINK_INSPECTOR
		[SpritePreview]
#endif
		[SerializeField] internal Sprite xboxIcon = null;

#if UNITY_EDITOR && THREADLINK_INSPECTOR
		[SpritePreview]
#endif
		[SerializeField] internal Sprite dualsenseIcon = null;
	}
}
