namespace Threadlink.Templates.UIUtilities
{
	using Core;
	using UnityEngine;
	using UnityEngine.UI;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	[RequireComponent(typeof(ThreadlinkText))]
	public class AutoLayoutText : LinkableBehaviour, IBootable
	{
		public ThreadlinkText Label => targetLabel;

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected ThreadlinkText targetLabel = null;

		protected override void Reset()
		{
			base.Reset();

			TryGetComponent(out targetLabel);
		}

		public override void Discard()
		{
			targetLabel.Discard();
			targetLabel = null;

			base.Discard();
		}

		public virtual void Boot()
		{
			targetLabel.OnValueChanged += RefreshLayout;
		}

		public virtual void Refresh()
		{
			RefreshLayout();
		}

		private void RefreshLayout(string _ = null)
		{
			const RectTransform.Axis HORIZONTAL = RectTransform.Axis.Horizontal;
			const RectTransform.Axis VERTICAL = RectTransform.Axis.Vertical;

			var rect = cachedTransform as RectTransform;

			rect.SetSizeWithCurrentAnchors(HORIZONTAL, LayoutUtility.GetPreferredWidth(rect));
			rect.SetSizeWithCurrentAnchors(VERTICAL, LayoutUtility.GetPreferredHeight(rect));
		}
	}
}
