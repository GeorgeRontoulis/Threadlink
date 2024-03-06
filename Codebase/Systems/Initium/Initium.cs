namespace Threadlink.Systems.Initium
{
	using System.Collections;
	using System.Collections.Generic;
	using Threadlink.Core;
	using UnityEngine;

	public sealed class Initium : LinkableBehaviourSingleton<Initium>
	{
		public override void Boot() { Instance = this; }
		public override void Initialize() { }

		private static IEnumerator WaitForOneFrame()
		{
			yield return Threadlink.WaitForFrameCount(1);
		}

		internal static InitializableCollection GetSceneInitCollection(string sceneName)
		{
			InitializableCollection[] candiates = FindObjectsByType<InitializableCollection>
			(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

			int length = candiates.Length;

			for (int i = 0; i < length; i++)
			{
				if (candiates[i].gameObject.scene.name.Equals(sceneName))
					return candiates[i];
			}

			return null;
		}

		#region Bootup And Initialization:
		public static IEnumerator BootAndInitCollection(InitializableCollection target)
		{
			yield return target.BootObjects();
			yield return target.InitializeObjects();
		}

		public static IEnumerator BootAndInit<T>(T target) where T : ILinkable
		{
			yield return Boot(target);
			yield return Initialize(target);
		}

		public static IEnumerator BootAndInit<T>(params T[] targets) where T : ILinkable
		{
			yield return Boot(targets);
			yield return Initialize(targets);
		}

		public static IEnumerator BootAndInit<T>(List<T> targets) where T : ILinkable
		{
			yield return Boot(targets);
			yield return Initialize(targets);
		}
		#endregion

		#region Bootup Only:
		public static IEnumerator Boot<T>(T target) where T : ILinkable
		{
			target.Boot();
			yield return WaitForOneFrame();
		}

		public static IEnumerator Boot<T>(params T[] targets) where T : ILinkable
		{
			int length = targets.Length;
			for (int i = 0; i < length; i++) yield return Boot(targets[i]);
		}

		public static IEnumerator Boot<T>(List<T> targets) where T : ILinkable
		{
			int length = targets.Count;
			for (int i = 0; i < length; i++) yield return Boot(targets[i]);
		}
		#endregion

		#region InitializationOnly:
		public static IEnumerator Initialize<T>(T target) where T : ILinkable
		{
			target.Initialize();
			yield return WaitForOneFrame();
		}

		public static IEnumerator Initialize<T>(params T[] targets) where T : ILinkable
		{
			int length = targets.Length;
			for (int i = 0; i < length; i++) yield return Initialize(targets[i]);
		}

		public static IEnumerator Initialize<T>(List<T> targets) where T : ILinkable
		{
			int length = targets.Count;
			for (int i = 0; i < length; i++) yield return Initialize(targets[i]);
		}
		#endregion
	}
}