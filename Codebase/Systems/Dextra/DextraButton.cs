namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
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
	public sealed class DextraButton : LinkableBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
	{
		public UnityEvent OnClick => button.onClick;
		public UnityEvent OnSelect => onSelect;
		public UnityEvent OnDeselect => onDeselect;

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private Button button = null;

		[SerializeField] private UnityEvent onSelect = new();
		[SerializeField] private UnityEvent onDeselect = new();

		protected override void Reset()
		{
			TryGetComponent(out button);
			base.Reset();
		}

		public override void Discard()
		{
			button.onClick.RemoveAllListeners();
			onSelect.RemoveAllListeners();
			onDeselect.RemoveAllListeners();
			onSelect = null;
			onDeselect = null;
			button = null;
		}

		public override void Boot() { }
		public override void Initialize() { }

		public void OnPointerEnter(PointerEventData eventData)
		{
			Dextra.SelectUIElement(button.gameObject);
		}

		void ISelectHandler.OnSelect(BaseEventData eventData)
		{
			onSelect.Invoke();
			Dextra.SyncSelection();
		}

		void IDeselectHandler.OnDeselect(BaseEventData eventData)
		{
			onDeselect.Invoke();
		}
	}
}