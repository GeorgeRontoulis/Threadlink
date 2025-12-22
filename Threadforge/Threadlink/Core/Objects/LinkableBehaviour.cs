namespace Threadlink.Core
{
    using Shared;
    using System;
    using UnityEngine;

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

        [HideInInspector, SerializeField] protected Transform cachedTransform = null;

        protected virtual void OnValidate()
        {
            var self = transform;

            if (cachedTransform != self)
                cachedTransform = self;
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