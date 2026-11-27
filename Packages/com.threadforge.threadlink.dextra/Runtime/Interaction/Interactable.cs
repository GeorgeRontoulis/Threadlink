namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.Localization;

    /// <summary>
    /// Base class for something the player can interact with. Override <see cref="InteractAsync"/>, and optionally
    /// <see cref="CanInteract"/> and <see cref="Prompt"/>. An <see cref="InteractionSensor"/> on the player finds it
    /// through its colliders.
    /// </summary>
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        [Tooltip("Text shown while this is the selected target: an entry of a string table.")]
        [SerializeField]
        private LocalizedString prompt = new LocalizedString();

        private readonly Guid identity = Guid.NewGuid();

        /// <summary>
        /// Text shown while this is the selected target, as a string table entry. Bind it to UI, or read it with
        /// <see cref="LocalizedString.GetLocalizedString()"/>, so it follows the selected language.
        /// </summary>
        public virtual LocalizedString Prompt => prompt;

        Guid IInteractable.Identity => identity;

        /// <summary>Whether <paramref name="actor"/> may interact now. Unavailable targets are never selected.</summary>
        public virtual bool CanInteract(GameObject actor) => isActiveAndEnabled;

        /// <summary>Performs the interaction. The sensor prevents overlapping interactions.</summary>
        protected abstract UniTask InteractAsync(GameObject actor, CancellationToken token);

        InteractionAvailability IInteractable.Evaluate(object actor) =>
            new InteractionAvailability(this != null && CanInteract(actor as GameObject), Prompt);

        UniTask IInteractable.ExecuteAsync(object actor, CancellationToken token) => InteractAsync(actor as GameObject, token);
    }

    /// <summary>
    /// Finds interactables around its GameObject every frame and selects the closest available one. Call
    /// <see cref="InteractAsync"/> from input; read <see cref="Prompt"/> for the UI.
    /// </summary>
    [AddComponentMenu("Threadlink/Dextra/Interaction Sensor")]
    [DisallowMultipleComponent]
    public sealed class InteractionSensor : MonoBehaviour
    {
        [Tooltip("How far interactables are found, in meters.")]
        [SerializeField, Min(0.01f)]
        private float radius = 2f;

        [Tooltip("Layers searched for interactable colliders.")]
        [SerializeField]
        private LayerMask layers = ~0;

        [Tooltip("Search 2D physics instead of 3D.")]
        [SerializeField]
        private bool use2D;

        private readonly Interactor interactor = new Interactor();
        private readonly List<InteractionCandidate> candidates = new List<InteractionCandidate>();
        private readonly Collider[] colliders = new Collider[32];
        private readonly Collider2D[] colliders2D = new Collider2D[32];
        private IInteractable announced;

        /// <summary>The selected target, or null.</summary>
        public IInteractable Target => interactor.Selected;

        /// <summary>The selected target's prompt, or null when nothing is selected.</summary>
        public LocalizedString Prompt => interactor.Evaluate(gameObject).Prompt;

        /// <summary>Whether an interaction is running.</summary>
        public bool IsInteracting => interactor.IsExecuting;

        private void Update()
        {
            candidates.Clear();
            if (use2D)
                PhysicsTargets.Collect2D(transform.position, radius, layers, colliders2D, candidates);
            else
                PhysicsTargets.Collect3D(transform.position, radius, layers, colliders, candidates);
            interactor.Select(candidates, gameObject);
            if (!ReferenceEquals(announced, interactor.Selected))
            {
                announced = interactor.Selected;
                if (Iris.TryGetSingleton(out _))
                    Iris.Publish(new InteractionTargetChanged(this, announced));
            }
        }

        /// <summary>Interacts with the selected target. Returns false when nothing is selected or an interaction is running.</summary>
        public async UniTask<bool> InteractAsync()
        {
            var target = interactor.Selected;
            if (target == null || interactor.IsExecuting)
                return false;
            await interactor.ExecuteAsync(target.Identity, gameObject, destroyCancellationToken);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.62f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }

    /// <summary>Published on Iris when a sensor's selected target changes.</summary>
    public readonly struct InteractionTargetChanged
    {
        /// <summary>The sensor.</summary>
        public readonly InteractionSensor Sensor;

        /// <summary>The new target, or null.</summary>
        public readonly IInteractable Target;

        internal InteractionTargetChanged(InteractionSensor sensor, IInteractable target)
        {
            Sensor = sensor;
            Target = target;
        }
    }
}
