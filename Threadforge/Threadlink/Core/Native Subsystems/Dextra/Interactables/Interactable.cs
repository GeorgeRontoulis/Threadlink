namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Core;
    using Iris;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Flags;

    public abstract class Interactable3D : Interactable
    {
        protected internal override bool ActiveState
        {
            get => activeArea.enabled;
            set => activeArea.enabled = value;
        }

        [HideInInspector, SerializeField] protected Collider activeArea = null;

        protected override void OnValidate()
        {
            var area = GetComponent<Collider>();

            if (activeArea != area)
                activeArea = area;

            base.OnValidate();
        }

        protected override void DiscardActiveArea()
        {
            if (activeArea != null)
            {
                activeArea.enabled = false;
                activeArea = null;
            }
        }
    }

    public abstract class Interactable2D : Interactable
    {
        protected internal override bool ActiveState
        {
            get => activeArea.enabled;
            set => activeArea.enabled = value;
        }

        [HideInInspector, SerializeField] protected Collider2D activeArea = null;

        protected override void OnValidate()
        {
            var area = GetComponent<Collider2D>();

            if (activeArea != area)
                activeArea = area;

            base.OnValidate();
        }

        protected override void DiscardActiveArea()
        {
            if (activeArea != null)
            {
                activeArea.enabled = false;
                activeArea = null;
            }
        }
    }

    public abstract class Interactable : LinkableBehaviour
    {
        private const Iris.Events ON_INTERACT_EVENT = Iris.Events.OnInteract;

        protected internal abstract bool ActiveState { get; set; }
        public string InteractionPrompt => configuration.InteractionPrompt;

        [SerializeField] protected InteractableConfig configuration = null;

        protected abstract void DiscardActiveArea();

        public override void Discard()
        {
            UnsubscribeFromInteraction();
            DiscardActiveArea();

            base.Discard();
        }

        protected internal bool TryGetConfigAs<T>(out T result) where T : InteractableConfig
        {
            if (configuration != null && configuration is T config)
            {
                result = config;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Execute this interactable's logic.
        /// </summary>
        /// <returns><see langword="true"/> if the interaction happened. <see langword="false"/> otherwise.</returns>
        protected internal abstract bool Interact();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void OnDetected()
        {
            if (configuration.InteractionOptions.HasFlagUnsafe(InteractionOptions.InteractOnContact))
                Interact();
            else
                Iris.Subscribe<Func<bool>>(ON_INTERACT_EVENT, Interact);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void OnOutOfRange()
        {
            if (!configuration.InteractionOptions.HasFlagUnsafe(InteractionOptions.InteractOnContact))
                UnsubscribeFromInteraction();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UnsubscribeFromInteraction() => Iris.Unsubscribe<Func<bool>>(ON_INTERACT_EVENT, Interact);
    }
}