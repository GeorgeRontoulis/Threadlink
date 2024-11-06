namespace Threadlink.Core
{
	using MassTransit;
	using System;
	using Systems;
	using UnityEngine;
	using Utilities.Events;

	namespace ExtensionMethods
	{
		public static class LinkableAssetExtensionMethods
		{
			public static T Clone<T>(this T original) where T : LinkableAsset
			{
				var copy = UnityEngine.Object.Instantiate(original);

				copy.name = original.name;
				copy.IsInstance = true;

				return copy;
			}
		}
	}

	/// <summary>
	/// Base class for all Threadlink-Compatible assets.
	/// </summary>
	public abstract class LinkableAsset : ScriptableObject, IDiscardable
	{
		public virtual string LinkID => name;
		public virtual NewId InstanceID { get; set; }

		public event ThreadlinkDelegate<Empty, Empty> OnDiscard
		{
			add => onDiscard.OnInvoke += value;
			remove => onDiscard.OnInvoke -= value;
		}

		public bool IsInstance { get; internal set; }

		[NonSerialized] private VoidEvent onDiscard = new();

		public virtual Empty Discard(Empty _ = default)
		{
			if (onDiscard != null)
			{
				onDiscard.Invoke();
				onDiscard.Discard();

				if (IsInstance) onDiscard = null;
			}

			if (IsInstance) Destroy(this);
			return default;
		}

		public static T Create<T>(string assetName) where T : LinkableAsset
		{
			if (string.IsNullOrEmpty(assetName))
				Threadlink.Instance.SystemLog<InvalidLinkableAssetNameException>();

			var output = CreateInstance<T>();

			output.name = assetName;
			output.IsInstance = true;

			return output;
		}
	}
}
