namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;
	using UnityEngine;
	using UnityEngine.UI;

	public sealed class DextraInputPrompt : LinkableBehaviour
	{
		[SerializeField] private Image promptImage = null;
		[SerializeField] private Text promptLabel = null;

		[SerializeField] private DextraInputPromptData data = null;

		public override void Discard()
		{
			Dextra.OnInputDeviceChanged.Remove(OnDeviceChanged);
			promptImage = null;
			promptLabel = null;
			data = null;
			base.Discard();
		}

		public override void Boot() { }
		public override void Initialize()
		{
			OnDeviceChanged();
			Dextra.OnInputDeviceChanged.TryAddListener(OnDeviceChanged);
		}

		public VoidOutput OnDeviceChanged(Dextra.InputDevice currentInputDevice = 0)
		{
			UpdateGraphics(Dextra.CurrentInputDevice);
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
			promptLabel.text = data.promptText;
		}
	}
}
