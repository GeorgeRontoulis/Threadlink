namespace Threadlink.Core
{
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// Base class for all Threadlink-Compatible assets. All assets compatible with Threadlink should derive from this class.
	/// </summary>
	public abstract class LinkableAsset : ScriptableObject
	{
		public event VoidDelegate OnBeforeDiscarded
		{
			add { if (onBeforeDiscarded.Contains(value) == false) onBeforeDiscarded += value; }
			remove { onBeforeDiscarded -= value; }
		}

		public bool Initialized { get; private set; }
		public string ID { get; private set; }

		private event VoidDelegate onBeforeDiscarded = null;

		public abstract void Boot();

		public virtual void Initialize() { Initialized = true; }

		public virtual void Discard()
		{
			onBeforeDiscarded?.Invoke();

			ID = null;
			onBeforeDiscarded = null;
			Destroy(this);
		}

		public void SetID() { ID = string.Empty; }
	}
}
