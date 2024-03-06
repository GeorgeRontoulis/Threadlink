namespace Threadlink.Systems
{
	using Core;
	using Utilities.Events;
	using VoidDelegate = Utilities.Events.ThreadlinkDelegate<Utilities.Events.VoidOutput, Utilities.Events.VoidInput>;

	/// <summary>
	/// System responsible for broadcasting Update notifications 
	/// on all subscribed Threadlink-Compatible listeners.
	/// </summary>
	public sealed class Iris : LinkableBehaviourSingleton<Iris>
	{
		public static VoidEvent OnUpdate => Instance.onUpdate;
		public static VoidEvent OnFixedUpdate => Instance.onFixedUpdate;
		public static VoidEvent OnLateUpdate => Instance.onLateUpdate;

		public static bool UpdateSelf { get; set; }

		private VoidEvent onUpdate = new();
		private VoidEvent onFixedUpdate = new();
		private VoidEvent onLateUpdate = new();

		private void OnDestroy()
		{
			DiscardUpdateCallbacks();
		}

		public override void Discard()
		{
			DiscardUpdateCallbacks();
			base.Discard();
		}

		public override void Boot() { Instance = this; }
		public override void Initialize() { UpdateSelf = true; }

		public static void SubscribeToUpdate(VoidDelegate action) { OnUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromUpdate(VoidDelegate action) { OnUpdate?.Remove(action); }
		public static void SubscribeToFixedUpdate(VoidDelegate action) { OnFixedUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromFixedUpdate(VoidDelegate action) { OnFixedUpdate?.Remove(action); }
		public static void SubscribeToLateUpdate(VoidDelegate action) { OnLateUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromLateUpdate(VoidDelegate action) { OnLateUpdate?.Remove(action); }

		private void Update() { if (UpdateSelf) onUpdate.Invoke(); }
		private void FixedUpdate() { if (UpdateSelf) onFixedUpdate.Invoke(); }
		private void LateUpdate() { if (UpdateSelf) onLateUpdate.Invoke(); }

		private void DiscardUpdateCallbacks()
		{
			onUpdate.Discard();
			onFixedUpdate.Discard();
			onLateUpdate.Discard();

			onUpdate = null;
			onFixedUpdate = null;
			onLateUpdate = null;
		}
	}
}