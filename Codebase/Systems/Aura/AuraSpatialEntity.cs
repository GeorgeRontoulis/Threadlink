namespace Threadlink.Systems.Aura
{
	using Core;
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#if UNITY_EDITOR
	using Threadlink.Utilities.Editor;
	using Threadlink.Utilities.Events;
#endif
	using UnityEngine;

	public abstract class AuraSpatialEntity : LinkableBehaviour
	{
		protected abstract Vector3 SourcePosition { get; }

		public float Volume { set => source.volume = value; }
		protected float Radius { get; set; }

#if ODIN_INSPECTOR
		[Required]
#endif
		[SerializeField] protected AudioSource source = null;
		[Range(0f, 1f)][SerializeField] protected float radiusCoefficient = 1f;
		[Range(0f, 1f)][SerializeField] protected float influence = 1f;

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (EditorUtilities.EditorInOrWillChangeToPlaymode && Radius != source.maxDistance * radiusCoefficient)
			{
				Radius = source.maxDistance * radiusCoefficient;
			}
		}
#endif

		public override void Boot()
		{
			Radius = source.maxDistance * radiusCoefficient;
		}

		public override void Initialize()
		{
		}

		protected override void Reset()
		{
			base.Reset();

			bool found = TryGetComponent(out AudioSource source);

			if (found)
			{
				source.volume = 1;
				source.loop = source.playOnAwake = true;
			}
		}

		public override VoidOutput Discard(VoidInput _ = default)
		{
			if (source.isPlaying) source.Stop();
			source.clip = null;
			source = null;
			return base.Discard(_);
		}

		public virtual float GetSpatialInfluence(Vector3 listenerPosition)
		{
			float distance = Vector3.Distance(listenerPosition, SourcePosition);

			// Inverse distance influence
			return Mathf.Clamp(distance >= Radius ? 0f : Mathf.Clamp01(1f - (distance / Radius)), 0f, influence);
		}
	}
}