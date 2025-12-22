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
        protected abstract Selectable UnitySelectable { get; }

        protected internal event Action<DextraSelectable> OnSelected = null;
        protected internal event Action<DextraSelectable> OnDeselected = null;

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
        public virtual void OnPointerEnter(PointerEventData eventData) => Dextra.Instance.SelectUIElement(gameObject).Forget();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnSelect(BaseEventData eventData) => OnSelected?.Invoke(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnDeselect(BaseEventData eventData) => OnDeselected?.Invoke(this);

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
        protected override Selectable UnitySelectable => selectable;

        [HideInInspector, SerializeField] protected T selectable = null;

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
