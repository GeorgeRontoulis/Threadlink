namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Iris;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    [RequireComponent(typeof(Rigidbody))]
    public abstract class EntityDetector3D<ActiveAreaType, EntityType> : EntityDetector<EntityType>
    where ActiveAreaType : Collider
    where EntityType : LinkableBehaviour
    {
        protected internal override bool ActiveState
        {
            get => activeArea.enabled;
            set => activeArea.enabled = value;
        }

        [HideInInspector, SerializeField] protected ActiveAreaType activeArea = null;

        protected override void OnValidate()
        {
            var area = GetComponent<ActiveAreaType>();

            if (activeArea != area)
                activeArea = area;

            if (TryGetComponent(out Rigidbody rb))
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            base.OnValidate();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (DetectionFilter(other, out var entity))
                OnEntityDetected(entity);
        }

        private void OnTriggerExit(Collider other)
        {
            if (OutOfRangeFilter(other, out var entity))
                OnEntityOutOfRange(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual bool DetectionFilter(Collider other, out EntityType entity) => other.TryGetComponent(out entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual bool OutOfRangeFilter(Collider other, out EntityType entity) => other.TryGetComponent(out entity);
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EntityDetector2D<ActiveAreaType, EntityType> : EntityDetector<EntityType>
    where ActiveAreaType : Collider2D
    where EntityType : LinkableBehaviour
    {
        protected internal override bool ActiveState
        {
            get => activeArea.enabled;
            set => activeArea.enabled = value;
        }

        [HideInInspector, SerializeField] protected ActiveAreaType activeArea = null;

        protected override void OnValidate()
        {
            var area = GetComponent<ActiveAreaType>();

            if (activeArea != area)
                activeArea = area;

            if (TryGetComponent(out Rigidbody rb))
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            base.OnValidate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (DetectionFilter2D(other, out var entity))
                OnEntityDetected(entity);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (OutOfRangeFilter2D(other, out var entity))
                OnEntityOutOfRange(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual bool DetectionFilter2D(Collider2D other, out EntityType entity) => other.TryGetComponent(out entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual bool OutOfRangeFilter2D(Collider2D other, out EntityType entity) => other.TryGetComponent(out entity);
    }

    public abstract class EntityDetector<EntityType> : EntityDetector where EntityType : LinkableBehaviour
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void OnEntityDetected(EntityType entity) => Iris.Publish(OnEntityDetectedEvent, entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void OnEntityOutOfRange(EntityType entity) => Iris.Publish(OnEntityOutOfRangeEvent, entity);
    }

    public abstract class EntityDetector : LinkableBehaviour
    {
        protected internal abstract bool ActiveState { get; set; }
        protected internal abstract Iris.Events OnEntityDetectedEvent { get; }
        protected internal abstract Iris.Events OnEntityOutOfRangeEvent { get; }
    }
}
