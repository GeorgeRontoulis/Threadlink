namespace Threadlink.Systems.Initium
{
	using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using System.Linq;
	using Core;
	using UnityEngine;
	using Utilities.Events;

#pragma warning disable IDE0051
#pragma warning disable UNT0006

	public interface IRootInitializer { }

	public sealed class InitializableCollection : LinkableBehaviourSingleton<InitializableCollection>
	{
		private enum InitializationSequence { Threadlink, Unity }

		[SerializeField] private InitializationSequence initializationSequence = 0;

		[Space(10)]

		[SerializeField] private LinkableBehaviour[] objects = new LinkableBehaviour[0];

#if UNITY_EDITOR
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Find Linkable Objects")]
#endif
		private void FindLinkableObjects()
		{
			objects = FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Include,
			FindObjectsSortMode.None).Where(o => o.Equals(this) == false && o is IRootInitializer).ToArray();
		}
#endif

		private async UniTask Start()
		{
			if (initializationSequence.Equals(InitializationSequence.Threadlink)) return;

			await BootObjects();
			await InitializeObjects();
		}

		public override void Boot() { Instance = this; }
		public override void Initialize() { }

		internal async UniTask BootObjects()
		{
			await Initium.Boot(objects);
		}

		internal async UniTask InitializeObjects()
		{
			await Initium.Initialize(objects);
		}

		public override VoidOutput Discard(VoidInput _ = default)
		{
			objects = null;
			return base.Discard(_);
		}
	}
}