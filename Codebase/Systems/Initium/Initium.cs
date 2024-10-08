namespace Threadlink.Systems.Initium
{
	using Core;
	using Cysharp.Threading.Tasks;
	using System.Collections.Generic;
	using UnityEngine;

	public sealed class Initium : LinkableBehaviourSingleton<Initium>
	{
		public override void Initialize() { }

		internal static bool GetSceneInitCollection(string sceneName, out InitializableCollection result)
		{
			var candiates = FindObjectsByType<InitializableCollection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

			if (candiates == null)
			{
				result = null;
				return false;
			}

			int length = candiates.Length;

			for (int i = 0; i < length; i++)
			{
				var candidate = candiates[i];

				if (candidate.gameObject.scene.name.Equals(sceneName))
				{
					result = candidate;
					return true;
				}
			}

			result = null;
			return false;
		}

		private static async UniTask WaitForOneFrame()
		{
			await Threadlink.WaitForFrames(1);
		}

		#region Bootup And Initialization:
		public static async UniTask BootAndInitCollection(InitializableCollection target)
		{
			await target.BootObjects();
			await target.InitializeObjects();
		}

		public static async UniTask BootAndInit<T>(T target) where T : ILinkable
		{
			await Boot(target);
			await Initialize(target);
		}

		public static async UniTask BootAndInit<T>(params T[] targets) where T : ILinkable
		{
			await Boot(targets);
			await Initialize(targets);
		}

		public static async UniTask BootAndInit<T>(List<T> targets) where T : ILinkable
		{
			await Boot(targets);
			await Initialize(targets);
		}
		#endregion

		#region Bootup Only:
		public static async UniTask Boot<T>(T target) where T : ILinkable
		{
			target.Boot();
			await WaitForOneFrame();
		}

		public static async UniTask Boot<T>(params T[] targets) where T : ILinkable
		{
			int length = targets.Length;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = Boot(targets[i]);

			await UniTask.WhenAll(tasks);
		}

		public static async UniTask Boot<T>(List<T> targets) where T : ILinkable
		{
			int length = targets.Count;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = Boot(targets[i]);

			await UniTask.WhenAll(tasks);
		}
		#endregion

		#region InitializationOnly:
		public static async UniTask Initialize<T>(T target) where T : ILinkable
		{
			target.Initialize();
			await WaitForOneFrame();
		}

		public static async UniTask Initialize<T>(params T[] targets) where T : ILinkable
		{
			int length = targets.Length;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = Initialize(targets[i]);

			await UniTask.WhenAll(tasks);
		}

		public static async UniTask Initialize<T>(List<T> targets) where T : ILinkable
		{
			int length = targets.Count;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = Initialize(targets[i]);

			await UniTask.WhenAll(tasks);
		}
		#endregion
	}
}