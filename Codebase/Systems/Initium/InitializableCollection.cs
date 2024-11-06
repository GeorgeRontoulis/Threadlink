namespace Threadlink.Systems.Initium
{
	using Core;
	using Cysharp.Threading.Tasks;
	using System.Linq;
	using UnityEngine;
	using Utilities.Events;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public sealed class InitializableCollection : LinkableBehaviour, IThreadlinkSingleton<InitializableCollection>
	{
		public static InitializableCollection Instance { get; private set; }

		private enum InitializationSequence { Threadlink, Unity }

		[SerializeField] private InitializationSequence initializationSequence = 0;

		[Space(10)]

		[SerializeField] private LinkableBehaviour[] entities = new LinkableBehaviour[0];

#if UNITY_EDITOR
#pragma warning disable IDE0051
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Find Linkable Objects")]
#endif
		private void FindLinkableEntities()
		{
			entities = FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Include,
			FindObjectsSortMode.None).Where(o => o is IInitiumDiscoverable).ToArray();
		}
#endif

		public override Empty Discard(Empty _ = default)
		{
			if (entities != null) NullifyEntitiesArray();
			return base.Discard(_);
		}

		public void Boot() { Instance = this; }

#pragma warning disable UNT0006
		private async UniTaskVoid Start()
		{
			if (initializationSequence.Equals(InitializationSequence.Threadlink)) return;

			await BootObjects();
			await InitializeObjects();
		}

		internal async UniTask BootObjects() { await Initium.Boot(entities); }
		internal async UniTask InitializeObjects()
		{
			await Initium.Initialize(entities);

			NullifyEntitiesArray();
		}

		private void NullifyEntitiesArray()
		{
			int length = entities.Length;

			for (int i = 0; i < length; i++) entities[i] = null;

			entities = null;
		}
	}
}