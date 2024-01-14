namespace Threadlink.Utilities.Physics
{
	using UnityEngine;

	public struct CheckboxInfo
	{
		public Vector3 Center { get; private set; }
		public Vector3 HalfExtents { get; private set; }
		public Quaternion Orientation { get; private set; }
		public int LayerMask { get; private set; }
		public QueryTriggerInteraction TriggerInteraction { get; private set; }

		public CheckboxInfo(Vector3 halfExtents, Quaternion orientation)
		{
			Center = Vector3.zero;
			HalfExtents = halfExtents;
			Orientation = orientation;
			LayerMask = Physics.AllLayers;
			TriggerInteraction = QueryTriggerInteraction.Ignore;
		}

		public CheckboxInfo(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction triggerInteraction)
		{
			Center = center;
			HalfExtents = halfExtents;
			Orientation = orientation;
			LayerMask = layerMask;
			TriggerInteraction = triggerInteraction;
		}
	}

	public static class PhysicsOperations
	{
		public static bool CheckBox(CheckboxInfo info)
		{
			return Physics.CheckBox(info.Center, info.HalfExtents, info.Orientation, info.LayerMask, info.TriggerInteraction);
		}
	}
}