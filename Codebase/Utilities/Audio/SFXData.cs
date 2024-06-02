namespace Threadlink.Utilities.Audio
{
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Audio Utilities/SFX Data")]
	public sealed class SFXData : ScriptableObject
	{
		public float Duration => clip.length;

		public AudioClip clip = null;
		[Range(0f, 1f)] public float volume = 1;
		[Tooltip("Use this property to check whether this clip " +
		"should interrupt the currently playing sound in your implementation.")]
		public bool highPriority = false;
	}
}