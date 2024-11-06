namespace Threadlink.Systems.Dextra
{
	using Core;
	using Utilities.Events;
	using UnityEngine;
	using UnityEngine.Events;
	using UnityEngine.EventSystems;
	using UnityEngine.UI;
	using Cysharp.Threading.Tasks;
	using System;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Utilities.Editor.Attributes;
#endif

	[RequireComponent(typeof(Button))]
	public class DextraButton : LinkableBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
	{
		public UnityEvent OnClick => button.onClick;
		public GenericInputEvent<DextraButton> OnSelect => onSelect;
		public GenericInputEvent<DextraButton> OnDeselect => onDeselect;

		protected virtual bool SyncSelection => true;

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] protected Button button = null;

		[NonSerialized] protected GenericInputEvent<DextraButton> onSelect = new();
		[NonSerialized] protected GenericInputEvent<DextraButton> onDeselect = new();

		protected override void Reset()
		{
			TryGetComponent(out button);
			base.Reset();
		}

		public override Empty Discard(Empty _ = default)
		{
			button.onClick.RemoveAllListeners();
			onSelect.Discard();
			onDeselect.Discard();
			onSelect = null;
			onDeselect = null;
			button = null;
			return base.Discard(_);
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			Dextra.SelectUIElement(button.gameObject, SyncSelection).Forget();
		}

		void ISelectHandler.OnSelect(BaseEventData eventData)
		{
			onSelect.Invoke(this);
			if (SyncSelection) Dextra.SyncSelection();
		}

		void IDeselectHandler.OnDeselect(BaseEventData eventData)
		{
			onDeselect.Invoke(this);
		}
	}
}