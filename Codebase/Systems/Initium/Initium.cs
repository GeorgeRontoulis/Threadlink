namespace Threadlink.Systems.Initium
{
	using System.Collections;
	using System.Collections.Generic;
	using Threadlink.Core;
	using UnityEngine;

	public sealed class Initium : LinkableSystem<LinkableEntity>
	{
		private static Initium Instance { get; set; }

		public override void Boot()
		{
			Instance = this;
			base.Boot();
		}

		public override void Initialize()
		{
			Nexus.Nexus.OnBeforeSceneUnload += SeverAll;
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

		public static IEnumerator BootAndInitCollection(InitializableCollection target)
		{
			yield return target.BootObjects();
			yield return target.InitializeObjects();
		}

		public static IEnumerator BootAndInitLinkableObject(LinkableObject target)
		{
			yield return BootLinkableObject(target);
			yield return InitializeLinkableObject(target);
		}

		public static IEnumerator BootAndInitLinkableAsset(LinkableAsset target)
		{
			yield return BootLinkableAsset(target);
			yield return InitializeLinkableAsset(target);
		}

		public static IEnumerator BootLinkableObject(LinkableObject target)
		{
			target.Boot();
			yield return Threadlink.WaitForFrameCount(1);
		}

		public static IEnumerator InitializeLinkableObject(LinkableObject target)
		{
			target.Initialize();
			yield return Threadlink.WaitForFrameCount(1);
		}

		public static IEnumerator BootLinkableAsset(LinkableAsset target)
		{
			target.Boot();
			yield return Threadlink.WaitForFrameCount(1);
		}

		public static IEnumerator InitializeLinkableAsset(LinkableAsset target)
		{
			target.Initialize();
			yield return Threadlink.WaitForFrameCount(1);
		}

		public static IEnumerator BootLinkableObjects(params LinkableObject[] target)
		{
			int length = target.Length;

			for (int i = 0; i < length; i++) yield return BootLinkableObject(target[i]);
		}

		public static IEnumerator InitializeLinkableObjects(params LinkableObject[] target)
		{
			int length = target.Length;

			for (int i = 0; i < length; i++) yield return InitializeLinkableObject(target[i]);
		}

		public static IEnumerator BootLinkableObjects<T>(List<T> target) where T : LinkableObject
		{
			int length = target.Count;

			for (int i = 0; i < length; i++) yield return BootLinkableObject(target[i]);
		}

		public static IEnumerator InitializeLinkableObjects<T>(List<T> target) where T : LinkableObject
		{
			int length = target.Count;

			for (int i = 0; i < length; i++) yield return InitializeLinkableObject(target[i]);
		}
	}
}