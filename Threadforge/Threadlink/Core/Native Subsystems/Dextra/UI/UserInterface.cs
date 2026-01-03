namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Chronos;
    using Iris;
    using Shared;
    using System;
    using Utilities.Mathematics;
    using UnityEngine;

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UserInterface : LinkableBehaviour
    {
        public bool IsVisible => canvasGroup.alpha.IsSimilarTo(1f);
        public bool IsHidden => canvasGroup.alpha.IsSimilarTo(0f);
        public bool UpdatingAlpha { get; private set; }
        private float TargetAlpha { get; set; }

        [HideInInspector, SerializeField] private CanvasGroup canvasGroup = null;

        protected override void OnValidate()
        {
            base.OnValidate();

            var group = GetComponent<CanvasGroup>();

            if (canvasGroup != group)
                canvasGroup = group;
        }

        public override void Discard()
        {
            Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
            canvasGroup = null;
            base.Discard();
        }

        private void UpdateAlpha(float newAlpha)
        {
            TargetAlpha = newAlpha;
            UpdatingAlpha = true;
            Iris.Subscribe<Action>(Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
        }

        private void MoveTowardsTargetAlpha()
        {
            canvasGroup.alpha = canvasGroup.alpha.MoveTowards(TargetAlpha, 4 * Chronos.Instance.UnscaledDeltaTime);

            if (canvasGroup.alpha.IsSimilarTo(TargetAlpha))
            {
                Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
                canvasGroup.alpha = TargetAlpha;
                UpdatingAlpha = false;
            }
        }

        internal void SetInteractableState(bool state)
        {
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }

        protected void Display() => UpdateAlpha(1f);
        protected void Hide() => UpdateAlpha(0f);

        internal void ForceCanvasGroupAlphaTo(float alpha)
        {
            UpdatingAlpha = true;
            TargetAlpha = alpha;
            canvasGroup.alpha = alpha;
            UpdatingAlpha = false;
        }

        /// <summary>
        /// Called when this UI becomes the active (topmost) one.
        /// </summary>
        protected internal virtual void OnStacked()
        {
            Display();

            if (this is IInteractableInterface)
                SetInteractableState(true);
        }

        /// <summary>
        /// Called when another UI is stacked on top of this one.
        /// </summary>
        protected internal virtual void OnCovered()
        {
            if (this is not IPersistentInterface)
                Hide();

            SetInteractableState(false);
        }

        /// <summary>
        /// Called when this UI becomes the active (topmost) one again, after having been covered by another.
        /// </summary>
        protected internal virtual void OnResurfaced()
        {
            Display();

            if (this is IInteractableInterface)
                SetInteractableState(true);
        }

        /// <summary>
        /// Called when this UI is completely removed from the stack, usually when getting cancelled etc.
        /// </summary>
        protected internal virtual void OnPopped()
        {
            Hide();
            SetInteractableState(false);
        }
    }

    public abstract class UserInterface<S> : UserInterface, IThreadlinkSingleton<S>
    where S : UserInterface<S>
    {
        public static S Instance { get; private set; }

        public virtual void Boot() => Instance = this as S;
    }
}