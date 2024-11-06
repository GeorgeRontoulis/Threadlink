namespace Threadlink.Systems
{
	using Core;
	using IrisDelegate = Utilities.Events.ThreadlinkDelegate<Utilities.Events.Empty, Utilities.Events.Empty>;

	/// <summary>
	/// System responsible for broadcasting Update notifications
	/// to all subscribed Threadlink-Compatible listeners.
	/// </summary>
	public sealed class Iris : ThreadlinkSystem<Iris>
	{
		private static ThreadlinkEventBus EventBus => Threadlink.EventBus;

		public static event IrisDelegate OnUpdate
		{
			add => EventBus.OnIrisUpdate += value;
			remove => EventBus.OnIrisUpdate -= value;
		}

		public static event IrisDelegate OnFixedUpdate
		{
			add => EventBus.OnIrisFixedUpdate += value;
			remove => EventBus.OnIrisFixedUpdate -= value;
		}

		public static event IrisDelegate OnLateUpdate
		{
			add => EventBus.OnIrisLateUpdate += value;
			remove => EventBus.OnIrisLateUpdate -= value;
		}

		private void Update() { EventBus.InvokeOnIrisUpdateEvent(); }
		private void FixedUpdate() { EventBus.InvokeOnIrisFixedUpdateEvent(); }
		private void LateUpdate() { EventBus.InvokeOnIrisLateUpdateEvent(); }
	}
}