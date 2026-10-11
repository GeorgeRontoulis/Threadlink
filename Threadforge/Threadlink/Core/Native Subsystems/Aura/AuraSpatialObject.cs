namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Core;
    using Unity.Mathematics;
    using UnityEngine;

    public abstract class AuraSpatialObject : LinkableBehaviour
    {
        protected abstract Vector3 SourcePosition { get; }

        protected internal float Volume
        {
            set
            {
                var audioSource = ResolveSource();

                if (audioSource != null)
                    audioSource.volume = value;
            }
        }

        /// <summary>
        /// The influence radius: the source's maximum distance scaled by <see cref="radiusCoefficient"/>. It is computed
        /// from serialized data whenever it is read, so it holds in builds as it does in the Editor. A derived type may
        /// assign its own radius instead.
        /// </summary>
        protected float Radius
        {
            get
            {
                if (radiusOverride.HasValue)
                    return radiusOverride.Value;

                var audioSource = ResolveSource();
                return audioSource != null ? audioSource.maxDistance * radiusCoefficient : 0f;
            }
            set => radiusOverride = value;
        }

        [SerializeField] protected AudioSource source = null;
        [Range(0f, 1f), SerializeField] protected float radiusCoefficient = 1f;
        [Range(0f, 1f), SerializeField] protected float influence = 1f;

        private float? radiusOverride = null;

        protected override void OnValidate()
        {
            if (TryGetComponent(out AudioSource attachedSource))
            {
                attachedSource.volume = 1;
                attachedSource.loop = attachedSource.playOnAwake = true;

                if (source == null)
                    source = attachedSource;
            }

            base.OnValidate();
        }

        public override void Discard()
        {
            // Unlink before destruction, so that Aura never evaluates a destroyed object. Nexus discards every spatial
            // object in a scene before unloading it, so this is all the teardown Aura's links need.
            if (Aura.TryGetSingleton(out var aura)
            && aura.TryGetLinkedObject<AuraSpatialObject>(ID, out var linkedObject)
            && linkedObject == this)
            {
                aura.TryDisconnect<AuraSpatialObject>(ID, out _);
            }

            if (source != null)
            {
                if (source.isPlaying) source.Stop();
                source.clip = null;
                source = null;
            }

            base.Discard();
        }

        protected internal virtual float GetSpatialInfluence(Vector3 listenerPosition)
        {
            float radius = Radius;
            float distance = Vector3.Distance(listenerPosition, SourcePosition);

            // Inverse distance influence
            return math.clamp(distance >= radius ? 0f : math.clamp(1f - (distance / radius), 0f, 1f), 0f, influence);
        }

        /// <summary>The assigned source, or else the AudioSource on this GameObject.</summary>
        private AudioSource ResolveSource()
        {
            if (source == null)
                TryGetComponent(out source);

            return source;
        }
    }
}
