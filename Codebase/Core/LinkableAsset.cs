namespace Threadlink.Core
{
	using System;
	using Systems;
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// Base class for all Threadlink-Compatible assets.
	/// </summary>
	public abstract class LinkableAsset : ScriptableObject, ILinkable
	{
		public virtual string LinkID => name;
		public bool IsInstance { get; internal set; }

		public VoidEvent OnBeforeDiscarded => onBeforeDiscarded;

		[NonSerialized] private VoidEvent onBeforeDiscarded = new();

		public abstract void Boot();
		public abstract void Initialize();

		public virtual void Discard()
		{
			if (onBeforeDiscarded != null)
			{
				onBeforeDiscarded.Invoke();
				onBeforeDiscarded.Discard();

				if (IsInstance) onBeforeDiscarded = null;
			}

			if (IsInstance) Destroy(this);
		}

		public static T Create<T>(string assetName) where T : LinkableAsset
		{
			if (string.IsNullOrEmpty(assetName))
			{
				Scribe.LogException(new NullReferenceException("Linkable Asset name cannot be null or empty!"));
				return null;
			}

			var output = CreateInstance<T>();

			output.name = assetName;
			output.IsInstance = true;

			return output;
		}
	}
}
