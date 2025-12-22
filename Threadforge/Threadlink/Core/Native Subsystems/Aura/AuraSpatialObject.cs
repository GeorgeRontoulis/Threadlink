namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Core;
    using UnityEngine;

    public abstract class AuraSpatialObject : LinkableBehaviour
    {
        protected abstract Vector3 SourcePosition { get; }

        protected internal float Volume { set => source.volume = value; }
        protected float Radius { get; set; }

        [SerializeField] protected AudioSource source = null;
        [Range(0f, 1f), SerializeField] protected float radiusCoefficient = 1f;
        [Range(0f, 1f), SerializeField] protected float influence = 1f;

        protected override void OnValidate()
        {
            if (TryGetComponent(out AudioSource source))
            {
                source.volume = 1;
                source.loop = source.playOnAwake = true;
            }

            if (source != null)
            {
                float target = source.maxDistance * radiusCoefficient;

                if (!Mathf.Approximately(Radius, target))
                    Radius = target;
            }

            base.OnValidate();
        }

        public override void Discard()
        {
            if (source.isPlaying) source.Stop();
            source.clip = null;
            source = null;
            base.Discard();
        }

        protected internal virtual float GetSpatialInfluence(Vector3 listenerPosition)
        {
            float distance = Vector3.Distance(listenerPosition, SourcePosition);

            // Inverse distance influence
            return Mathf.Clamp(distance >= Radius ? 0f : Mathf.Clamp01(1f - (distance / Radius)), 0f, influence);
        }
    }
}
