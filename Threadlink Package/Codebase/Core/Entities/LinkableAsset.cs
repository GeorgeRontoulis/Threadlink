namespace Threadlink.Core
{
	using ExtensionMethods;
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

				copy.Calibrate(original.name);

				return copy;
			}

			internal static void Calibrate<T>(this T copy, string name) where T : LinkableAsset
			{
				copy.name = name;
				copy.LinkID = Ulid.NewUlid();
				copy.IsInstance = true;
			}

			internal static void Calibrate<T>(this T copy, string name, Ulid linkID) where T : LinkableAsset
			{
				copy.name = name;
				copy.LinkID = linkID;
				copy.IsInstance = true;
			}
		}
	}

	/// <summary>
	/// Base class for all Threadlink-Compatible assets.
	/// </summary>
	public abstract class LinkableAsset : ScriptableObject, IDiscardable, ILinkable<Ulid>, ILinkable<string>
	{
		public virtual Ulid LinkID { get; set; }
		string ILinkable<string>.LinkID { get => name; set => name = value; }
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
			if (string.IsNullOrEmpty(assetName))
			{
				throw new ArgumentException(
				Scribe.FromSubsystem<Threadlink>("A ", nameof(LinkableAsset), "'s name cannot be NULL or empty!").ToString());
			}

			var output = CreateInstance<T>();

			output.Calibrate(assetName);

			return output;
		}

		public static T Create<T>(string assetName, Ulid linkID) where T : LinkableAsset
		{
			if (string.IsNullOrEmpty(assetName))
			{
				throw new ArgumentException(
				Scribe.FromSubsystem<Threadlink>("A ", nameof(LinkableAsset), "'s name cannot be NULL or empty!").ToString());
			}

			var output = CreateInstance<T>();

			output.Calibrate(assetName, linkID);

			return output;
		}
	}
}
