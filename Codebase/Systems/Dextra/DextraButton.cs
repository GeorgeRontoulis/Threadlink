namespace Threadlink.Systems.Dextra
{
	using System;
	using Threadlink.Core;
	using Threadlink.Utilities.Events;
	using UnityEngine;
	using UnityEngine.Events;
	using UnityEngine.EventSystems;
	using UnityEngine.UI;

	[Serializable]
	public sealed class DextraButtonEvent<T>
	{
		public event GenericVoidDelegate<T> CSharpAction
		{
			add
			{
				if (cSharpAction == null) cSharpAction += value;
				else if (cSharpAction.Contains(value) == false) cSharpAction += value;
			}
			remove { if (cSharpAction != null) cSharpAction -= value; }
		}

		public event UnityAction UnityEvent
		{
			add { unityEvent.AddListener(value); }
			remove { unityEvent.RemoveListener(value); }
		}

		private event GenericVoidDelegate<T> cSharpAction = null;

		[SerializeField] private UnityEvent unityEvent = new();

		public void Discard()
		{
			RemoveAllListeners();
			unityEvent = null;
		}

		public void RemoveAllListeners()
		{
			cSharpAction = null;
			unityEvent.RemoveAllListeners();
		}

		public void Invoke(T argument = default)
		{
			unityEvent?.Invoke();
			cSharpAction?.Invoke(argument);
		}
	}

	public sealed class DextraButton : Selectable, ISelectHandler, IDeselectHandler, IPointerClickHandler, IPointerEnterHandler
	{
		public DextraButtonEvent<DextraButton> OnClickEvent { get => onClick; }
		public DextraButtonEvent<DextraButton> OnSelectEvent { get => onSelect; }
		public DextraButtonEvent<DextraButton> OnDeselectEvent { get => onDeselect; }

		[SerializeField] private DextraButtonEvent<DextraButton> onClick = new();
		[SerializeField] private DextraButtonEvent<DextraButton> onSelect = new();
		[SerializeField] private DextraButtonEvent<DextraButton> onDeselect = new();

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

		void ISelectHandler.OnSelect(BaseEventData eventData)
		{
			onSelect.Invoke(this);
			Dextra.SyncSelection();
		}

		void IDeselectHandler.OnDeselect(BaseEventData eventData)
		{
			onDeselect.Invoke(this);
		}
	}
}