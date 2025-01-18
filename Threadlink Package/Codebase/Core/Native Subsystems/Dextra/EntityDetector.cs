namespace Threadlink.Core.Subsystems.Dextra
{
	using Propagator;
	using UnityEngine;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	[RequireComponent(typeof(Rigidbody))]
	public abstract class EntityDetector3D<ActiveAreaType, EntityType> : EntityDetector<EntityType>
	where ActiveAreaType : Collider
	where EntityType : LinkableBehaviour
	{
		public override bool ActiveState { get => activeArea.enabled; set => activeArea.enabled = value; }

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private ActiveAreaType activeArea = null;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out activeArea);
		}

		private void OnTriggerEnter(Collider other)
		{
			if (DetectionFilter(other, out var entity)) OnEntityDetected(entity);
		}

		private void OnTriggerExit(Collider other)
		{
			if (OutOfRangeFilter(other, out var entity)) OnEntityOutOfRange(entity);
		}

		public virtual bool DetectionFilter(Collider other, out EntityType entity) => other.TryGetComponent(out entity);
		public virtual bool OutOfRangeFilter(Collider other, out EntityType entity) => other.TryGetComponent(out entity);
	}

	[RequireComponent(typeof(Rigidbody2D))]
	public abstract class EntityDetector2D<ActiveAreaType, EntityType> : EntityDetector<EntityType>
	where ActiveAreaType : Collider2D
	where EntityType : LinkableBehaviour
	{
		public override bool ActiveState { get => activeArea.enabled; set => activeArea.enabled = value; }

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private ActiveAreaType activeArea = null;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out activeArea);
		}

		private void OnTriggerEnter2D(Collider2D collision)
		{
			if (DetectionFilter2D(collision, out var entity)) OnEntityDetected(entity);
		}

		private void OnTriggerExit2D(Collider2D collision)
		{
			if (OutOfRangeFilter2D(collision, out var entity)) OnEntityOutOfRange(entity);
		}

		public virtual bool DetectionFilter2D(Collider2D other, out EntityType entity) => other.TryGetComponent(out entity);
		public virtual bool OutOfRangeFilter2D(Collider2D other, out EntityType entity) => other.TryGetComponent(out entity);
	}

	public abstract class EntityDetector<EntityType> : EntityDetector where EntityType : LinkableBehaviour
	{
		public virtual void OnEntityDetected(EntityType entity) => Propagator.Publish(OnEntityDetectedEvent, entity);
		public virtual void OnEntityOutOfRange(EntityType entity) => Propagator.Publish(OnEntityOutOfRangeEvent, entity);
	}

	public abstract class EntityDetector : LinkableBehaviour
	{
		public abstract bool ActiveState { get; set; }
		public abstract PropagatorEvents OnEntityDetectedEvent { get; }
		public abstract PropagatorEvents OnEntityOutOfRangeEvent { get; }
	}
}
