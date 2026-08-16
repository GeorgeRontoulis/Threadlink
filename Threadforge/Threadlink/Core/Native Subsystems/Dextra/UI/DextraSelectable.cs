namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [DisallowMultipleComponent]
    public abstract class DextraSelectable : LinkableBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public abstract Selectable UnitySelectable { get; }

        public event Action<DextraSelectable> OnSelected = null;
        public event Action<DextraSelectable> OnDeselected = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            OnSelected = null;
            OnDeselected = null;
            base.Discard();
        }

        /// <summary>
        /// UX-unifying method to force mouse hovering into selecting the element instead,
        /// providing a smooth navigation experience for gamepads.
        /// </summary>
        /// <param name="eventData">The event data payload.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (Dextra.TryGetSingleton(out var dextra))
                dextra.SelectUIElement(gameObject).Forget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnSelect(BaseEventData eventData)
        {
            OnSelected?.Invoke(this);

            if (Dextra.TryGetSingleton(out var dextra))
                dextra.HandleNativeSelection(gameObject).Forget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnDeselect(BaseEventData eventData)
        {
            OnDeselected?.Invoke(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal bool TryGetUnitySelectable<S>(out S result) where S : Selectable
        {
            if (UnitySelectable is S convertedSelectable)
            {
                result = convertedSelectable;
                return true;
            }

            result = null;
            return false;
        }
    }

    [DisallowMultipleComponent]
    public abstract class DextraSelectable<T> : DextraSelectable where T : Selectable
    {
        public override Selectable UnitySelectable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => selectable;
        }

        public T GenericUnitySelectable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => selectable;
        }

        [HideInInspector, SerializeField] protected T selectable = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            selectable = null;
            base.Discard();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            var selfSelectable = GetComponent<T>();

            if (selectable != selfSelectable)
                selectable = selfSelectable;
        }
    }
}
