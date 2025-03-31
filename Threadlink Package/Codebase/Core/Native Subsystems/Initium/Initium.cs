namespace Threadlink.Core.Subsystems.Initium
{
	using Core;
	using Cysharp.Threading.Tasks;
	using System.Collections.Generic;
	using UnityEngine;

	public interface IInitiumDiscoverable { }

	public static class Initium
	{
		private static async UniTask OneFrame() => await Threadlink.WaitForFrames(1);

		internal static bool TryGetInitializableCollection(out InitializableCollection result)
		{
			var collection = Object.FindAnyObjectByType<InitializableCollection>(FindObjectsInactive.Exclude);

			result = collection;
			return collection != null;
		}

		public static async UniTask BootAndInitCollectionAsync(InitializableCollection collection)
		{
			collection.Boot();

			await collection.BootObjects();
			await collection.InitializeObjects();
		}

		public static async UniTask BootAndInitAsync<T>(T entity) where T : IBootable, IInitializable
		{
			await BootAsync(entity);
			await InitializeAsync(entity);
		}

		public static async UniTask BootAsync(IBootable entity)
		{
			entity.Boot();
			await OneFrame();
		}

		public static async UniTask InitializeAsync(IInitializable entity)
		{
			entity.Initialize();
			await OneFrame();
		}

		public static void BootAndInit<T>(T entity) where T : IBootable, IInitializable
		{
			Boot(entity);
			Initialize(entity);
		}

		public static async UniTask BootAndInit<T>(IReadOnlyList<T> entities)
		{
			await Boot(entities);
			await Initialize(entities);
		}

		public static async UniTask Boot<T>(IReadOnlyList<T> entities)
		{
			int length = entities.Count;

			for (int i = 0; i < length; i++)
			{
				if (entities[i] is IBootable entity)
				{
					entity.Boot();
					await OneFrame();
				}
			}
		}

		public static async UniTask Initialize<T>(IReadOnlyList<T> entities)
		{
			int length = entities.Count;

			for (int i = 0; i < length; i++)
			{
				if (entities[i] is IInitializable entity)
				{
					entity.Initialize();
					await OneFrame();
				}
			}
		}

		public static void Boot(IBootable entity)
		{
			entity.Boot();
		}

		public static void Initialize(IInitializable entity)
		{
			entity.Initialize();
		}
	}
}