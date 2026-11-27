namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Designer data: a place that lowers audio categories as the listener approaches it, such as music fading under a
    /// waterfall or a campfire's crackle. Optionally plays a looping cue at its position while it is enabled.
    /// </summary>
    /// <remarks>
    /// A category is lowered by its <see cref="Lowering.Amount"/> at the zone's center, by nothing at its radius, and in
    /// between in proportion to the distance. Where zones overlap, the strongest lowering of each category applies.
    /// </remarks>
    [AddComponentMenu("Threadlink/Aura/Audio Zone")]
    public sealed class AudioZone : MonoBehaviour
    {
        /// <summary>How much one category is lowered.</summary>
        [Serializable]
        public struct Lowering
        {
            /// <summary>The category.</summary>
            [AudioCategoryName]
            [Tooltip("The category lowered near the zone.")]
            public string Category;

            /// <summary>The fraction removed at the zone's center, from 0 to 1.</summary>
            [Range(0, 1)]
            [Tooltip("How much of the category's volume is removed at the zone's center (1 silences it).")]
            public float Amount;
        }

        [Tooltip("Optional sound played at the zone while it is enabled. Use a looping cue.")]
        [SerializeField]
        private AudioCue cue;

        [Min(0.01f)]
        [Tooltip("The distance from the zone's center at which it stops having an effect.")]
        [SerializeField]
        private float radius = 10;

        [Tooltip("The categories lowered near the zone, such as Music.")]
        [SerializeField]
        private List<Lowering> lowers = new List<Lowering> { new Lowering { Category = "Music", Amount = 0.5f } };

        private PlaybackHandle voice;

        /// <summary>The distance at which the zone stops having an effect.</summary>
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.01f, value);
        }

        /// <summary>The categories the zone lowers.</summary>
        public List<Lowering> Lowers => lowers;

        /// <summary>
        /// How strongly the zone acts on a listener at <paramref name="listener"/>: 1 at its center, 0 at its radius and
        /// beyond. Each category is lowered by its amount times this.
        /// </summary>
        public float Strength(Vector3 listener) => Mathf.Clamp01(1 - Vector3.Distance(listener, transform.position) / radius);

        private void OnEnable()
        {
            AuraEngine.Zones.Add(this);
            if (cue != null && Aura.TryGetSingleton(out _))
            {
                try
                {
                    voice = Aura.Play(cue, transform);
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                }
            }
        }

        private void OnDisable()
        {
            AuraEngine.Zones.Remove(this);
            voice?.Stop();
            voice = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.62f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
