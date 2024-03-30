namespace Threadlink.Systems.Dextra
{
	using System;
	using Threadlink.Utilities.Events;
	using UnityEngine;
	using UnityEngine.Events;
	using UnityEngine.EventSystems;
	using UnityEngine.UI;

	[Serializable]
	public sealed class DextraButtonEvent
	{
		public VoidGenericEvent<DextraButton> CSharpAction => cSharpAction;

		public event UnityAction UnityEvent
		{
			add { unityEvent.AddListener(value); }
			remove { unityEvent.RemoveListener(value); }
		}

		private VoidGenericEvent<DextraButton> cSharpAction = new();

		[SerializeField] private UnityEvent unityEvent = new();

		public void Discard()
		{
			cSharpAction.Discard();
			RemoveAllListeners();
			cSharpAction = null;
			unityEvent = null;
		}

		public void RemoveAllListeners()
		{
			CSharpAction.Discard();
			unityEvent.RemoveAllListeners();
		}

		public void Invoke(DextraButton argument = default)
		{
			unityEvent?.Invoke();
			cSharpAction.Invoke(argument);
		}
	}

	public sealed class DextraButton : Selectable, ISelectHandler, IDeselectHandler, IPointerClickHandler, IPointerEnterHandler
	{
		public DextraButtonEvent OnClickEvent { get => onClick; }
		public DextraButtonEvent OnSelectEvent { get => onSelect; }
		public DextraButtonEvent OnDeselectEvent { get => onDeselect; }

		[SerializeField] private DextraButtonEvent onClick = new();
		[SerializeField] private DextraButtonEvent onSelect = new();
		[SerializeField] private DextraButtonEvent onDeselect = new();

		public void Discard()
		{
			onClick.Discard();
			onSelect.Discard();
			onDeselect.Discard();
			onClick = null;
			onSelect = null;
			onDeselect = null;
		}

		new public void OnPointerEnter(PointerEventData eventData)
		{
			Dextra.SelectUIElement(this);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			onClick.Invoke(this);
		}

		new public void OnSelect(BaseEventData eventData)
		{
			onSelect.Invoke(this);
			Dextra.SyncSelection();
		}

		new public void OnDeselect(BaseEventData eventData)
		{
			onDeselect.Invoke(this);
		}
	}
}