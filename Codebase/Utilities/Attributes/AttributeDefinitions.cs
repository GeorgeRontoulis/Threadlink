namespace Threadlink.Utilities.Editor.Attributes
{
	using UnityEngine;

	public class SpritePreviewAttribute : PropertyAttribute
	{
		public float PreviewHeight { get; private set; }

		public SpritePreviewAttribute(float previewHeight = 75)
		{
			PreviewHeight = previewHeight;
		}
	}

	public sealed class AddressableAssetButtonAttribute : PropertyAttribute { }
	public sealed class ReadOnlyAttribute : PropertyAttribute { }
}