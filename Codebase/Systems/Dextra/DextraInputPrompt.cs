namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
	using UnityEngine;
	using UnityEngine.UI;

	public sealed class DextraInputPrompt : LinkableEntity
	{
		[SerializeField] private Image promptImage = null;
		[SerializeField] private Text promptLabel = null;

		[SerializeField] private DextraInputPromptData data = null;

		public override void Discard()
		{
			Dextra.OnInputDeviceChanged -= UpdateGraphics;
			promptImage = null;
			promptLabel = null;
			data = null;
			base.Discard();
		}

		public override void Boot() { }
		public override void Initialize()
		{
			UpdateGraphics();
			Dextra.OnInputDeviceChanged += UpdateGraphics;
		}

		public void UpdateGraphics() { UpdateGraphics(Dextra.CurrentInputDevice); }

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
