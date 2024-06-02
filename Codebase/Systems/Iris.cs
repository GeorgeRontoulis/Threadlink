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
		private static VoidEvent OnUpdate { get; set; }
		private static VoidEvent OnFixedUpdate { get; set; }
		private static VoidEvent OnLateUpdate { get; set; }

		public static bool UpdateSelf { get; set; }

		private void OnDestroy()
		{
			DiscardUpdateCallbacks();
		}

		public override void Discard()
		{
			DiscardUpdateCallbacks();
			base.Discard();
		}

		public override void Boot()
		{
			OnUpdate ??= new();
			OnFixedUpdate ??= new();
			OnLateUpdate ??= new();
			Instance = this;
		}
		public override void Initialize() { UpdateSelf = true; }

		public static void SubscribeToUpdate(VoidDelegate action) { OnUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromUpdate(VoidDelegate action) { OnUpdate?.Remove(action); }
		public static void SubscribeToFixedUpdate(VoidDelegate action) { OnFixedUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromFixedUpdate(VoidDelegate action) { OnFixedUpdate?.Remove(action); }
		public static void SubscribeToLateUpdate(VoidDelegate action) { OnLateUpdate?.TryAddListener(action); }
		public static void UnsubscribeFromLateUpdate(VoidDelegate action) { OnLateUpdate?.Remove(action); }

		private void Update() { if (UpdateSelf) OnUpdate?.Invoke(); }
		private void FixedUpdate() { if (UpdateSelf) OnFixedUpdate?.Invoke(); }
		private void LateUpdate() { if (UpdateSelf) OnLateUpdate?.Invoke(); }

		private void DiscardUpdateCallbacks()
		{
			OnUpdate?.Discard();
			OnFixedUpdate?.Discard();
			OnLateUpdate?.Discard();

			OnUpdate = null;
			OnFixedUpdate = null;
			OnLateUpdate = null;
		}
	}
}