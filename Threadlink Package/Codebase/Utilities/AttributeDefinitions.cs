namespace Threadlink.Editor.Attributes
{
	using System;
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

	[AttributeUsage(AttributeTargets.Field)]
	public class MinMaxRangeAttribute : PropertyAttribute
	{
		#region Fields
		public readonly float MinLimit;
		public readonly float MaxLimit;
		public readonly uint Decimals;
		#endregion

		#region Setup
		/// <summary>
		/// A bounded range for integers.
		/// </summary>
		/// <param name="minLimit">The minimum acceptable value.</param>
		/// <param name="maxLimit">The maximum acceptable value.</param>
		public MinMaxRangeAttribute(int minLimit, int maxLimit)
		{
			MinLimit = minLimit;
			MaxLimit = maxLimit;
		}

		/// <summary>
		/// A bounded range for floats.
		/// </summary>
		/// <param name="minLimit">The minimum acceptable value.</param>
		/// <param name="maxLimit">The maximum acceptable value.</param>
		/// <param name="decimals">How many decimals the inspector labels should display. Values must be in the [0,3]
		/// range. Default is 1.</param>
		public MinMaxRangeAttribute(float minLimit, float maxLimit, uint decimals = 1)
		{
			MinLimit = minLimit;
			MaxLimit = maxLimit;
			Decimals = decimals;
		}
		#endregion
	}


	public sealed class AddressableAssetButtonAttribute : PropertyAttribute { }
	public sealed class ReadOnlyAttribute : PropertyAttribute { }
}