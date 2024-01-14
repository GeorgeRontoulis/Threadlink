namespace Threadlink.Core
{
	using Sirenix.OdinInspector;
	using Utilities.Collections;
	using UnityEngine;
	using Utilities.Editor;
	using Utilities.Events;

	/// <summary>
	/// Base class for all Threadlink-Compatible objects.
	/// Do not inherit from this class outside of Threadlink's scope. 
	/// Use LinkableEntity for scene objects instead.
	/// </summary>
	public abstract class LinkableObject : MonoBehaviour, IIdentifiable
	{
		public event VoidDelegate OnBeforeDiscarded
		{
			add { if (onBeforeDiscarded.Contains(value) == false) onBeforeDiscarded += value; }
			remove { onBeforeDiscarded -= value; }
		}

		public virtual string LinkID => name;

		public Transform SelfTransform => selfTransform;

		[ReadOnly][SerializeField] protected Transform selfTransform = null;

		private event VoidDelegate onBeforeDiscarded = null;

#if UNITY_EDITOR
		protected virtual void OnValidate()
		{
			if (EditorUtilities.EditorInOrWillChangeToPlaymode) return;

			this.TrySetAttachedComponent(ref selfTransform);
		}
#endif

		public abstract void Boot();
		public abstract void Initialize();

		/// <summary>
		/// Nullifies all fields of this LinkableObject and destroys it.
		/// You can use the OnBeforeDiscarded() event to get a callback before that happens.
		/// </summary>
		public virtual void Discard()
		{
			onBeforeDiscarded?.Invoke();

			selfTransform = null;
			onBeforeDiscarded = null;
			Destroy(gameObject);
		}
	}
}