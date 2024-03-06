namespace Threadlink.Systems.Anima
{
	using UnityEngine;
	using UnityEngine.Animations;
	using UnityEngine.Playables;

	/// <summary>
	/// You may inherit from this class to create custom injectables for the Anima Sub-System.
	/// </summary>
	[CreateAssetMenu(menuName = "Threadlink/Anima/Clip Injectable")]
	public class AnimaInjectable : ScriptableObject
	{
		[SerializeField] public AnimationClip clip = null;

		[Space(10)]

		[SerializeField] public AnimationCurve velocity = AnimationCurve.Constant(0, 1, 1);
		[SerializeField] public double playbackSpeed = 1;

		[Space(10)]

		[SerializeField] public bool applyPlayableIK = true;

		internal virtual Playable ToPlayable(AnimaRig rig)
		{
			var result = AnimationClipPlayable.Create(rig.Graph, clip);

			result.SetSpeed(playbackSpeed);
			result.SetApplyPlayableIK(applyPlayableIK);

			return result;
		}
	}
}