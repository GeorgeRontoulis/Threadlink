namespace Threadlink.Systems.Initium
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using System.Collections;
	using System.Linq;
	using Threadlink.Core;
	using UnityEngine;

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
			FindObjectsSortMode.None).Where(o => o.Equals(this) == false).ToArray();
		}
#endif

		private IEnumerator Start()
		{
			if (initializationSequence.Equals(InitializationSequence.Threadlink)) yield break;

			yield return BootObjects();
			yield return InitializeObjects();
		}

		public override void Boot() { Instance = this; }
		public override void Initialize() { }

		internal IEnumerator BootObjects()
		{
			yield return Initium.Boot(objects);
		}

		internal IEnumerator InitializeObjects()
		{
			yield return Initium.Initialize(objects);
		}

		public override void Discard()
		{
			objects = null;
			base.Discard();
		}
	}
}