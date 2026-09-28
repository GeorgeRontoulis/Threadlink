namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using global::Threadlink.Core.NativeSubsystems.Chronos;
    using global::Threadlink.Core.NativeSubsystems.Initium;
    using global::Threadlink.Core.NativeSubsystems.Iris;
    using global::Threadlink.Generated;
    using global::Threadlink.Shared;
    using global::Threadlink.Utilities.Mathematics;
    using global::Threadlink.Utilities.Objects;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Unity.Scripting.LifecycleManagement;
    using UnityEngine;

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UserInterface : LinkableBehaviour, IBootable
    {
        public bool IsVisible => canvasGroup.alpha.IsSimilarTo(1f);
        public bool IsHidden => canvasGroup.alpha.IsSimilarTo(0f);
        public bool UpdatingAlpha { get; private set; }
        protected float TargetAlpha { get; set; }
        protected float TargetFadeRate { get; set; }

        private float FadeInRate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float duration = Dextra.TryGetSingleton(out var dextra) && dextra.Config != null
                ?
                dextra.Config.UIFadeInDuration
                :
                NativeConstants.UI.DEFAULT_FADEIN_DURATION;
                return 1f / duration;
            }
        }

        private float FadeOutRate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float duration = Dextra.TryGetSingleton(out var dextra) && dextra.Config != null
                ?
                dextra.Config.UIFadeOutDuration
                :
                NativeConstants.UI.DEFAULT_FADEOUT_DURATION;
                return 1f / duration;
            }
        }

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

        /// <summary>
        /// The input icons in this interface, found when it boots: it boots and discards them, as
        /// <see cref="InteractableUserInterface{Singleton, Selectable}"/> discards its selectables.
        /// </summary>
        private List<DextraInputIcon> InputIcons { get; } = new(0);

        protected override void OnValidate()
        {
            base.OnValidate();

            this.Set(ref canvasGroup);
            this.Set(ref canvas);
        }

        public override void Discard()
        {
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, MoveTowardsTargetAlpha);
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnLateUpdate, ControlCanvas); //No op.
            DiscardInputIcons();
            canvasGroup = null;
            base.Discard();
        }

        public virtual void Boot()
        {
            if (canvas != null)
                Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnLateUpdate, ControlCanvas);

            BootInputIcons();
        }

        private void BootInputIcons()
        {
            GetComponentsInChildren(true, InputIcons);

            int count = InputIcons.Count;

            for (int i = 0; i < count; i++)
                Initium.Boot(InputIcons[i]);
        }

        private void DiscardInputIcons()
        {
            int count = InputIcons.Count;

            for (int i = 0; i < count; i++)
            {
                if (InputIcons[i] != null)
                    InputIcons[i].Discard();
            }

            InputIcons.Clear();
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
            canvasGroup.alpha = canvasGroup.alpha.MoveTowards(TargetAlpha, TargetFadeRate * Chronos.UnscaledDeltaTime);

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

        protected void Display()
        {
            TargetFadeRate = FadeInRate;
            UpdateAlpha(1f);
        }

        protected void Hide()
        {
            TargetFadeRate = FadeOutRate;
            UpdateAlpha(0f);
        }

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

    public abstract partial class UserInterface<S> : UserInterface, IThreadlinkSingleton<S>
    where S : UserInterface<S>
    {
        /// <summary>
        /// Reset when Play Mode starts or ends without a domain reload: interfaces are destroyed with Play Mode, and
        /// nothing discards them first.
        /// </summary>
        [AutoStaticsCleanup]
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