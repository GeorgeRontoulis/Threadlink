namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Propagator;
	using System;
	using UnityEngine;
	using UnityEngine.UI;

#if THREADLINK_INTEGRATIONS_TMPRO
	using TMPro;
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
			if (data != null) UpdateGraphics(previewDevice);
		}
#endif

		public override void Discard()
		{
			Propagator.Unsubscribe<Action<Dextra.InputDevice>>(PropagatorEvents.OnInputDeviceChanged, UpdateGraphics);
			promptImage = null;
			promptLabel = null;
			data = null;
			base.Discard();
		}

		public void Initialize()
		{
			UpdateGraphics(Dextra.CurrentInputDevice);
			Propagator.Subscribe<Action<Dextra.InputDevice>>(PropagatorEvents.OnInputDeviceChanged, UpdateGraphics);
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
