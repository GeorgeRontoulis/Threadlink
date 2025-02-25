namespace Threadlink.Templates.UIUtilities
{
	using Core;
	using System;
	using UnityEngine.UI;

	/// <summary>
	/// An extension of the UGUI Text Component, exposing an event invoked when the text is changed.
	/// </summary>
	public sealed class ThreadlinkText : Text, IDiscardable
	{
		public override string text
		{
			get => base.text;
			set
			{
				base.text = value;
				OnValueChanged?.Invoke(value);
			}
		}

		public event Action<string> OnValueChanged = null;

		public void Discard()
		{
			OnValueChanged = null;
		}

		public void Set(string newText) => text = newText;
	}
}
