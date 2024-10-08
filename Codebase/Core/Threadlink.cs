namespace Threadlink.Core
{
	using Cysharp.Threading.Tasks;
	using Extensions.Addressables;
	using System;
	using Systems;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;
	using Utilities.Editor.Attributes;
	using Utilities.Events;

	public static class ThreadlinkCoreExtensionMethods
	{
		public static T Clone<T>(this T original) where T : LinkableAsset
		{
			var copy = UnityEngine.Object.Instantiate(original);

			copy.name = original.name;
			copy.IsInstance = true;

			return copy;
		}
	}

	/// <summary>
	/// The Core Game System. Controls all aspects of the runtime
	/// and manages sub-systems and entities at the lowest level.
	/// </summary>
	public sealed class Threadlink : UnitySystem<Threadlink, LinkableBehaviour>
	{
		private static ThreadlinkAddressables Addressables => Instance.addressables;

		public override string LinkID => "Threadlink";

		[ReadOnly][SerializeField] private ThreadlinkAddressables addressables = null;

#pragma warning disable UNT0006
#pragma warning disable IDE0051

		#region Initialization and Deployment
		private void Awake() { Boot(); }

		private async UniTaskVoid Start()
		{
			Initialize();
			await Deploy();
		}

		public override void Initialize() { }

		private async UniTask Deploy()
		{
			var systems = addressables.coreSystems;
			int systemAddressablesCount = systems.Length;
			var uniTasks = new UniTask[systemAddressablesCount];

			for (int i = 0; i < systemAddressablesCount; i++) uniTasks[i] = systems[i].LoadAsync();

			await UniTask.WhenAll(uniTasks);

			for (int i = 0; i < systemAddressablesCount; i++)
			{
				var wovenSystem = Weave(new(systems[i].Result));

				if (wovenSystem is IAssetPreloader) await (wovenSystem as IAssetPreloader).PreloadAssetsAsync();
			}

			await Initium.Boot(LinkedEntities);
			await Initium.Initialize(LinkedEntities);

			Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Threadlink successfully deployed. All Systems operational.");
		}

		public static void ShutDown()
		{
			Instance.Discard();
		}

		public override VoidOutput Discard(VoidInput _ = default)
		{
			SeverAll();

			addressables = null;
			Instance = null;

#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
			return base.Discard(_);
		}
		#endregion

		public static async UniTask WaitForFrames(int frameCount)
		{
			for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
		}

		#region Addressables Methods
		private const string SearchNullAddressablesExtensionError =
		"A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!";

		public static void FindAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component
		{
			var extension = Addressables.customExtension;

			if (extension != null)
			{
				extension.SearchForAddressablePrefab<PrefabType>(prefabID, out var prefab);
				result = prefab;
				return;
			}
			else
			{
				Scribe.SystemLog<NullReferenceException>(Instance.LinkID, SearchNullAddressablesExtensionError);
				result = null;
			}
		}

		public static void FindAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : UnityEngine.Object
		{
			var extension = Addressables.customExtension;

			if (extension != null)
			{
				extension.SearchForAddressableAsset<AssetType>(assetID, out var asset);
				result = asset;
				return;
			}
			else
			{
				Scribe.SystemLog<NullReferenceException>(Instance.LinkID, SearchNullAddressablesExtensionError);
				result = null;
			}
		}

		public static void FindAddressableScene(string address, out AddressableScene result)
		{
			Addressables.scenes.BinarySearch(address, out var addressable);
			result = addressable;
		}

		public static bool TryGetCustomAddressablesExtension<T>(out T result) where T : ThreadlinkAddressablesExtension
		{
			var extension = Addressables.customExtension as T;

			if (extension == null)
			{
				Scribe.SystemLog<NullReferenceException>(Instance.LinkID, "No Custom Addressables Extension specified!");
				result = null;
				return false;
			}

			result = extension;
			return true;
		}
		#endregion
	}
}