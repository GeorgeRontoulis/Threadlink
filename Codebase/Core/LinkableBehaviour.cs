namespace Threadlink.Core
{
	using MassTransit;
	using System;
	using UnityEngine;
	using Utilities.Editor.Attributes;
	using Utilities.Events;

	/// <summary>
	/// Base class for all Threadlink-Compatible Components.
	/// </summary>
	[RequireComponent(typeof(Transform))]
	public abstract class LinkableBehaviour : MonoBehaviour, IDiscardable
	{
		public virtual string LinkID => name;
		public virtual NewId InstanceID { get; set; }

		public event ThreadlinkDelegate<Empty, Empty> OnDiscard
		{
			add => onDiscard.OnInvoke += value;
			remove => onDiscard.OnInvoke -= value;
		}

		public Transform SelfTransform => selfTransform;

		[ReadOnly][SerializeField] protected Transform selfTransform = null;

		[NonSerialized] private VoidEvent onDiscard = new();

		protected virtual void Reset()
		{
			TryGetComponent(out selfTransform);
		}

		public virtual Empty Discard(Empty _ = default)
		{
			if (onDiscard != null)
			{
				onDiscard.Invoke();
				onDiscard.Discard();

				onDiscard = null;
			}

			selfTransform = null;
			Destroy(gameObject);
			return default;
		}
	}
}