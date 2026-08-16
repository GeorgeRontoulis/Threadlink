namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Chronos;
    using Generated;
    using Iris;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Mathematics;
    using Utilities.Objects;

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UserInterface : LinkableBehaviour, IBootable
    {
        public bool IsVisible => canvasGroup.alpha.IsSimilarTo(1f);
        public bool IsHidden => canvasGroup.alpha.IsSimilarTo(0f);
        public bool UpdatingAlpha { get; private set; }
        private float TargetAlpha { get; set; }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ReadOnly]
#else
        [HideInInspector]
#endif
        [SerializeField] private CanvasGroup canvasGroup = null;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ReadOnly]
#else
        [HideInInspector]
#endif
        [SerializeField] private Canvas canvas = null;

        protected override void OnValidate()
        {
            base.OnValidate();

            this.Set(ref canvasGroup);
            this.Set(ref canvas);
        }

        public override void Discard()
        {
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnLateUpdate, ControlCanvas);
            canvasGroup = null;
            base.Discard();
        }

        public virtual void Boot()
        {
            Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnLateUpdate, ControlCanvas);
        }

        private void UpdateAlpha(float newAlpha)
        {
            TargetAlpha = newAlpha;

            if (!UpdatingAlpha)
            {
                UpdatingAlpha = true;
                Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
            }
        }

        private void MoveTowardsTargetAlpha()
        {
            canvasGroup.alpha = canvasGroup.alpha.MoveTowards(TargetAlpha, 4 * Chronos.UnscaledDeltaTime);

            if (canvasGroup.alpha.IsSimilarTo(TargetAlpha))
            {
                Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
                canvasGroup.alpha = TargetAlpha;
                UpdatingAlpha = false;
            }
        }

        private void ControlCanvas()
        {
            bool shouldEnable = canvasGroup.alpha > Mathf.Epsilon;

            if (canvas.enabled != shouldEnable)
                canvas.enabled = shouldEnable;
        }

        public void SetInteractableState(bool state)
        {
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }

        protected void Display() => UpdateAlpha(1f);
        protected void Hide() => UpdateAlpha(0f);

        public void ForceCanvasGroupAlphaTo(float alpha)
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
        protected static S Instance { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSingleton(out S result)
        {
            result = Instance ?? null;
            return result != null;
        }

        public override void Boot()
        {
            Instance = this as S;
            base.Boot();
        }
    }
}