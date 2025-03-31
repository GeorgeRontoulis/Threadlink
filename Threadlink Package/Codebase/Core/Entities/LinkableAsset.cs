namespace Threadlink.Core
{
	using Subsystems.Scribe;
	using System;
	using UnityEngine;

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
	public abstract class LinkableAsset : ScriptableObject, IDiscardable, IIdentifiable, INamable
	{
		public virtual int ID => GetInstanceID();
		public virtual string Name => name;

		public bool IsInstance { get; internal set; }

		public event Action OnDiscard = null;

		public virtual void Discard()
		{
			if (OnDiscard != null)
			{
				OnDiscard.Invoke();
				OnDiscard = null;
			}

			if (IsInstance) Destroy(this);
		}

		public static T Create<T>(string assetName) where T : LinkableAsset
		{
			if (string.IsNullOrEmpty(assetName)) PostInvalidAssetNameException();

			var output = CreateInstance<T>();

			output.name = assetName;
			output.IsInstance = true;

			return output;
		}

		private static void PostInvalidAssetNameException()
		{
			throw new ArgumentException(Scribe.FromSubsystem<Threadlink>(
			"A ", nameof(LinkableAsset), "'s name cannot be NULL or empty!").ToString());
		}
	}
}
