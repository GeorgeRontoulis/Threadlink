namespace Threadlink.Systems
{
	using Core;
	using Utilities.Events;

	/// <summary>
	/// System responsible for broadcasting Update notifications 
	/// on all subscribed Threadlink-Compatible listeners.
	/// </summary>
	public sealed class Iris : LinkableSystem<LinkableEntity>
	{
		public static Iris Instance { get; private set; }

		private event VoidDelegate onUpdate, onFixedUpdate, onLateUpdate;

		public static event VoidDelegate OnUpdate
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onUpdate;


				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onUpdate -= value; }
		}

		public static event VoidDelegate OnFixedUpdate
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onFixedUpdate;

				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onFixedUpdate -= value; }
		}

		public static event VoidDelegate OnLateUpdate
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onLateUpdate;

				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onLateUpdate -= value; }
		}

		public bool UpdateSelf { get; set; }

		public override void Boot()
		{
			Instance = this;
			base.Boot();
		}

		public override void Initialize()
		{
			UpdateSelf = true;
		}

		public static void SubscribeToUpdate(VoidDelegate action) { OnUpdate += action; }
		public static void UnsubscribeFromUpdate(VoidDelegate action) { OnUpdate -= action; }
		public static void SubscribeToFixedUpdate(VoidDelegate action) { OnFixedUpdate += action; }
		public static void UnsubscribeFromFixedUpdate(VoidDelegate action) { OnFixedUpdate -= action; }
		public static void SubscribeToLateUpdate(VoidDelegate action) { OnLateUpdate += action; }
		public static void UnsubscribeFromLateUpdate(VoidDelegate action) { OnLateUpdate -= action; }

		private void Update()
		{
			if (UpdateSelf == false) return;

			if (onUpdate != null) onUpdate();
		}

		private void FixedUpdate()
		{
			if (UpdateSelf == false) return;

			if (onFixedUpdate != null) onFixedUpdate();
		}

		private void LateUpdate()
		{
			if (UpdateSelf == false) return;

			if (onLateUpdate != null) onLateUpdate();
		}
	}
}