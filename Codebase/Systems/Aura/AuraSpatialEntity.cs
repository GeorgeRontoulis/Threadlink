namespace Threadlink.Systems.Aura
{
	using Core;
	using UnityEngine;
	using Utilities.Events;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#if UNITY_EDITOR
	using Utilities.Editor;
#endif

	public abstract class AuraSpatialEntity : LinkableBehaviour, IBootable
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

		public virtual void Boot()
		{
			Radius = source.maxDistance * radiusCoefficient;
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

		public override Empty Discard(Empty _ = default)
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
