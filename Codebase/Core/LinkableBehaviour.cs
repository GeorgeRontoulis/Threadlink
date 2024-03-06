namespace Threadlink.Core
{
	using System;
	using UnityEngine;
	using Utilities.Editor.Attributes;
	using Utilities.Events;

	/// <summary>
	/// Base class for all Threadlink-Compatible Components.
	/// </summary>
	[RequireComponent(typeof(Transform))]
	public abstract class LinkableBehaviour : MonoBehaviour, ILinkable
	{
		public virtual string LinkID => name;

		public Transform SelfTransform => selfTransform;
		public VoidEvent OnBeforeDiscarded => onBeforeDiscarded;

		[ReadOnly][SerializeField] protected Transform selfTransform = null;

		[NonSerialized] protected VoidEvent onBeforeDiscarded = new();

		protected virtual void Reset()
		{
			selfTransform = GetComponent<Transform>();
		}

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

			selfTransform = null;
			Destroy(gameObject);
		}
	}
}