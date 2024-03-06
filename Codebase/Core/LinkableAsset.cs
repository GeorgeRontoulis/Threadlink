namespace Threadlink.Core
{
	using System;
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

				onBeforeDiscarded = null;
			}

			Destroy(this);
		}
	}
}
