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

	public abstract class EntityDetector : LinkableBehaviour
	{
		public abstract bool ActiveState { get; set; }
		public abstract PropagatorEvents OnEntityDetectedEvent { get; }
		public abstract PropagatorEvents OnEntityOutOfRangeEvent { get; }
	}

	[RequireComponent(typeof(Rigidbody))]
	public abstract class EntityDetector<ActiveAreaType, EntityType> : EntityDetector
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

		public virtual void OnEntityDetected(EntityType entity)
		{
			Propagator.Publish(OnEntityDetectedEvent, entity);
		}

		public virtual void OnEntityOutOfRange(EntityType entity)
		{
			Propagator.Publish(OnEntityOutOfRangeEvent, entity);
		}

		public virtual bool DetectionFilter(Collider other, out EntityType entity)
		{
			return other.TryGetComponent(out entity);
		}

		public virtual bool OutOfRangeFilter(Collider other, out EntityType entity)
		{
			return other.TryGetComponent(out entity);
		}
	}
}
