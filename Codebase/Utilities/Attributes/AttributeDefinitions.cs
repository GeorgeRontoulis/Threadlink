namespace Threadlink.Utilities.Editor.Attributes
{
	using UnityEngine;

	public sealed class SpritePreviewAttribute : PropertyAttribute
	{
		public float PreviewHeight { get; private set; }

		public SpritePreviewAttribute(float previewHeight = 75)
		{
			PreviewHeight = previewHeight;
		}
	}

	public sealed class LabelledSliderAttribute : PropertyAttribute
	{
		public string propertyLabel;

		public float minValue;
		public float maxValue;

		public LabelledSliderAttribute(string propertyLabel, float minValue, float maxValue)
		{
			this.propertyLabel = propertyLabel;
			this.minValue = minValue;
			this.maxValue = maxValue;
		}
	}

	public sealed class AddressableAssetButtonAttribute : PropertyAttribute { }
	public sealed class ReadOnlyAttribute : PropertyAttribute { }
}