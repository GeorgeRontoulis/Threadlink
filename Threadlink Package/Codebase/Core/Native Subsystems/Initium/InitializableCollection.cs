namespace Threadlink.Core.Subsystems.Initium
{
	using Core;
	using Cysharp.Threading.Tasks;
	using System.Linq;
	using UnityEngine;

#if UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public sealed class InitializableCollection : LinkableBehaviourSingleton<InitializableCollection>
	{
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

		public override void Discard()
		{
			if (entities != null) NullifyEntitiesArray();
			base.Discard();
		}

#pragma warning disable UNT0006 // Incorrect message signature
		private async UniTaskVoid Start()
#pragma warning restore UNT0006 // Incorrect message signature
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