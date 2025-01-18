namespace Threadlink.Core.Subsystems.Aura
{
	using Core;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public abstract class AuraSpatialEntity : LinkableBehaviour
	{
		protected abstract Vector3 SourcePosition { get; }

		public float Volume { set => source.volume = value; }
		protected float Radius { get; set; }

#if UNITY_EDITOR && ODIN_INSPECTOR
		[Required]
#endif
		[SerializeField] protected AudioSource source = null;
		[Range(0f, 1f)][SerializeField] protected float radiusCoefficient = 1f;
		[Range(0f, 1f)][SerializeField] protected float influence = 1f;

#if UNITY_EDITOR
		private void OnValidate()
		{
			float target = source.maxDistance * radiusCoefficient;

			if (Mathf.Approximately(Radius, target) == false) Radius = target;
		}
#endif

		protected override void Reset()
		{
			base.Reset();

			if (TryGetComponent(out AudioSource source))
			{
				source.volume = 1;
				source.loop = source.playOnAwake = true;
			}
		}

		public override void Discard()
		{
			if (source.isPlaying) source.Stop();
			source.clip = null;
			source = null;
			base.Discard();
		}

		public virtual float GetSpatialInfluence(Vector3 listenerPosition)
		{
			float distance = Vector3.Distance(listenerPosition, SourcePosition);

			// Inverse distance influence
			return Mathf.Clamp(distance >= Radius ? 0f : Mathf.Clamp01(1f - (distance / Radius)), 0f, influence);
		}
	}
}
