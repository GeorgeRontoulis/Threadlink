namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Cysharp.Threading.Tasks;
	using System;
	using UnityEngine;
	using UnityEngine.Events;
	using UnityEngine.EventSystems;
	using UnityEngine.UI;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	[RequireComponent(typeof(Button))]
	public class DextraButton : LinkableBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
	{
		public UnityEvent OnClick => button.onClick;

		public event Action<DextraButton> OnSelect = null;
		public event Action<DextraButton> OnDeselect = null;

		protected virtual bool SyncSelection => true;

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected Button button = null;

		protected override void Reset()
		{
			TryGetComponent(out button);
			base.Reset();
		}

		public override void Discard()
		{
			button.onClick.RemoveAllListeners();
			OnSelect = null;
			OnDeselect = null;
			button = null;
			base.Discard();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			Dextra.SelectUIElement(button.gameObject, SyncSelection).Forget();
		}

		void ISelectHandler.OnSelect(BaseEventData eventData)
		{
			OnSelect?.Invoke(this);
			if (SyncSelection) Dextra.SyncSelection();
		}

		void IDeselectHandler.OnDeselect(BaseEventData eventData)
		{
			OnDeselect?.Invoke(this);
		}
	}
}