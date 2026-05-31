namespace Threadlink.Core
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Objects;

    /// <summary>
    /// Base class for all Threadlink-Compatible Components.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public abstract class LinkableBehaviour : MonoBehaviour, IDiscardable, IIdentifiable, INamable
    {
        public virtual int ID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetInstanceID();
        }

        public virtual string Name
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => name;
        }

        public Transform CachedTransform
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cachedTransform;
        }

        public event Action OnDiscard = null;

        [HideInInspector, SerializeField] protected Transform cachedTransform = null;

        protected virtual void OnValidate()
        {
            this.Set(ref cachedTransform);
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