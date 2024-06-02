namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;
	using UnityEngine;
	using UnityEngine.Events;
	using UnityEngine.EventSystems;
	using UnityEngine.UI;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Utilities.Editor.Attributes;
#endif

	[RequireComponent(typeof(Button))]
	public class DextraButton : LinkableBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
	{
		public UnityEvent OnClick => button.onClick;
		public VoidGenericEvent<DextraButton> OnSelect => onSelect;
		public VoidGenericEvent<DextraButton> OnDeselect => onDeselect;

		protected virtual bool SyncSelection => true;

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] protected Button button = null;

		[SerializeField] protected VoidGenericEvent<DextraButton> onSelect = new();
		[SerializeField] protected VoidGenericEvent<DextraButton> onDeselect = new();

		protected override void Reset()
		{
			TryGetComponent(out button);
			base.Reset();
		}

		public override void Discard()
		{
			button.onClick.RemoveAllListeners();
			onSelect.Discard();
			onDeselect.Discard();
			onSelect = null;
			onDeselect = null;
			button = null;
		}

		public override void Boot() { }
		public override void Initialize() { }

		public void OnPointerEnter(PointerEventData eventData)
		{
			Dextra.SelectUIElement(button.gameObject, SyncSelection);
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