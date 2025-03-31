namespace Threadlink.Core
{
	using System;
	using UnityEngine;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	/// <summary>
	/// Base class for all Threadlink-Compatible Components.
	/// </summary>
	[RequireComponent(typeof(Transform))]
	public abstract class LinkableBehaviour : MonoBehaviour, IDiscardable, IIdentifiable, INamable
	{
		public virtual int ID => GetInstanceID();
		public virtual string Name => name;
		public Transform CachedTransform => cachedTransform;

		public event Action OnDiscard = null;

#if UNITY_EDITOR && (THREADLINK_INSPECTOR || ODIN_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected Transform cachedTransform = null;

		protected virtual void Reset()
		{
			TryGetComponent(out cachedTransform);
		}

		public virtual void Discard()
		{
			if (OnDiscard != null)
			{
				OnDiscard.Invoke();
				OnDiscard = null;
			}

			cachedTransform = null;
			Destroy(gameObject);
		}

		public static T CreateFrom<T>(string name) where T : LinkableBehaviour
		{
			var behaviour = new GameObject(name, typeof(T)).GetComponent<T>();
			behaviour.cachedTransform = behaviour.transform;

			return behaviour;
		}
	}
}