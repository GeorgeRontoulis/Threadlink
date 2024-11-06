namespace Threadlink.Utilities.Events
{
	using Core;
	using System;
	using Systems;
	using UnityEngine;
	using UnityEngine.Events;

	public sealed class ScriptableEventListener : LinkableBehaviour, IBootable
	{
		[Serializable]
		public sealed class Configuration
		{
			[SerializeField] private ScriptableEvent eventAsset = null;
			[SerializeField] private UnityEvent reaction = new();

			private static void Throw() { Threadlink.Instance.SystemLog<NullScriptableEventException>(); }

			internal void Discard()
			{
				Unregister();
				reaction.RemoveAllListeners();
				eventAsset = null;
				reaction = null;
			}

			public void Register()
			{
				if (eventAsset == null) Throw(); else eventAsset.AddListener(reaction.Invoke);
			}

			public void Unregister()
			{
				if (eventAsset == null) Throw(); else eventAsset.RemoveListener(reaction.Invoke);
			}
		}

		public Configuration[] configurations = new Configuration[0];

		public override Empty Discard(Empty _ = default)
		{
			int length = configurations.Length;
			for (int i = 0; i < length; i++) configurations[i].Discard();
			return base.Discard(_);
		}

		public void Boot()
		{
			int length = configurations.Length;
			for (int i = 0; i < length; i++) configurations[i].Register();
		}
	}
}
