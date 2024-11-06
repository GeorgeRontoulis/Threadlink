namespace Threadlink.Systems.Dextra
{
	using Core;
	using Utilities.Events;
	using UnityEngine;
	using UnityEngine.UI;

#if THREADLINK_INTEGRATIONS_TMPRO
	using TMPro;
#endif

#if UNITY_EDITOR
	using Utilities.Editor;
#endif

	public sealed class DextraInputPrompt : LinkableBehaviour, IInitializable
	{
		public string PromptText { set => promptLabel.text = value; }

		[SerializeField] private DextraInputPromptData data = null;

		[Space(10)]

		[SerializeField] private Image promptImage = null;

#if THREADLINK_INTEGRATIONS_TMPRO
		[SerializeField] private TextMeshProUGUI promptLabel = null;
#else
		[SerializeField] private Text promptLabel = null;
#endif

#if UNITY_EDITOR
		[SerializeField] private Dextra.InputDevice previewDevice = 0;

		private void OnValidate()
		{
			if (EditorUtilities.EditorInOrWillChangeToPlaymode == false && data != null)
				UpdateGraphics(previewDevice);
		}
#endif

		public override Empty Discard(Empty _ = default)
		{
			Threadlink.EventBus.OnDextraDeviceChanged -= OnDeviceChanged;
			promptImage = null;
			promptLabel = null;
			data = null;
			return base.Discard(_);
		}

		public void Initialize()
		{
			OnDeviceChanged(Dextra.CurrentInputDevice);
			Threadlink.EventBus.OnDextraDeviceChanged += OnDeviceChanged;
		}

		public Empty OnDeviceChanged(Dextra.InputDevice currentInputDevice)
		{
			UpdateGraphics(currentInputDevice);
			return default;
		}

		private void UpdateGraphics(Dextra.InputDevice currentInputDevice)
		{
			switch (currentInputDevice)
			{
				case Dextra.InputDevice.MouseKeyboard:
				promptImage.sprite = data.mkbIcon;
				break;

				case Dextra.InputDevice.XBOXController:
				promptImage.sprite = data.xboxIcon;
				break;

				case Dextra.InputDevice.DualSense:
				promptImage.sprite = data.dualsenseIcon;
				break;
			}

			promptImage.type = Image.Type.Simple;
			promptImage.preserveAspect = true;
			if (promptLabel != null) promptLabel.text = data.promptText;
		}
	}
}
