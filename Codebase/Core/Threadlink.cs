namespace Threadlink.Core
{
	using Cysharp.Threading.Tasks;
	using Extensions.Addressables;
	using MassTransit;
	using System;
	using System.Collections.Generic;
	using Systems;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;
	using Utilities.Events;
	using UnityEngine.AddressableAssets;

#if THREADLINK_INSPECTOR
using Utilities.Editor.Attributes;
#elif ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	internal static class ThreadlinkDeployment
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static async UniTaskVoid DeployCore()
		{
			await Addressables.InitializeAsync();

			var handle = Addressables.LoadAssetAsync<GameObject>("Assets/Threadlink/Prefabs/Threadlink.prefab");

			await handle.ToUniTask();

			var threadlinkInstance = UnityEngine.Object.Instantiate(handle.Result).GetComponent<Threadlink>();
			threadlinkInstance.name = typeof(Threadlink).Name;

			if (threadlinkInstance.gameObject.scene.name.Equals("ThreadlinkScene") == false)
				threadlinkInstance.LogException<InvalidDeploymentException>();

			threadlinkInstance.Deploy().Forget();
		}
	}

	/// <summary>
	/// The Core Game System. Controls all aspects of the runtime
	/// and manages sub-systems and entities at the lowest level.
	/// </summary>
	public sealed class Threadlink : UnityWeaver<Threadlink, ThreadlinkSystem>
	{
		public static ThreadlinkEventBus EventBus => Instance.eventBus;

		private static ThreadlinkAddressables Addressables => Instance.addressables;

#if ODIN_INSPECTOR
		[ShowInInspector]
#endif
		[ReadOnly] private static readonly Dictionary<string, NewId> ConstantIDsBuffer = new();

		[ReadOnly, SerializeField] private ThreadlinkAddressables addressables = null;
		[ReadOnly, SerializeField] private ThreadlinkEventBus eventBus = null;

		public override Empty Discard(Empty _ = default)
		{
			Scribe.ResetExceptionPool();
			SeverAll();

			Destroy(eventBus);
			eventBus = null;
			addressables = null;
			Instance = null;

			ConstantIDsBuffer.Clear();
			ConstantIDsBuffer.TrimExcess();

#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
			return base.Discard(_);
		}

		#region Initialization and Deployment
		internal async UniTaskVoid Deploy()
		{
			eventBus = Instantiate(eventBus);
			ConstantIDsBuffer.Clear();
			Boot();

			var systems = addressables.coreSystems;
			int systemAddressablesCount = systems.Length;

			var uniTasks = new UniTask[systemAddressablesCount];

			for (int i = 0; i < systemAddressablesCount; i++) uniTasks[i] = systems[i].LoadAsync();

			await UniTask.WhenAll(uniTasks);

			for (int i = 0; i < systemAddressablesCount; i++)
			{
				var wovenSystem = Weave(systems[i].Result);

				await Initium.BootAsync(wovenSystem);

				if ((wovenSystem as IAssetPreloader) != null) await (wovenSystem as IAssetPreloader).PreloadAssetsAsync();
			}

			foreach (var subSystem in Registry.Values) await Initium.InitializeAsync(subSystem);

			this.SystemLog(Scribe.InfoNotif, "Core successfully deployed. All Sub-Systems operational.");
		}

		public override ThreadlinkSystem Weave(ThreadlinkSystem original)
		{
			var system = base.Weave(original);
			system.SelfTransform.SetParent(selfTransform);

			return system;
		}
		#endregion

		public static bool TryGetConstantSingletonID(string singletonName, out NewId result)
		{
			return ConstantIDsBuffer.TryGetValue(singletonName, out result);
		}

		public static bool TryRegisterConstantSingletonID(string singletonName, NewId result)
		{
			return ConstantIDsBuffer.TryAdd(singletonName, result);
		}

		public static void RegisterConstantSingletonID(string singletonName, NewId result)
		{
			ConstantIDsBuffer.Add(singletonName, result);
		}

		public static async UniTask WaitForFrames(int frameCount)
		{
			for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
		}

		#region Addressables Methods
		public static bool TryGetAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component
		{
			var extension = Addressables.customExtension;

			if (extension == null) Instance.SystemLog<SearchedNullAddressablesExtensionException>();

			return extension.TryGetAddressablePrefab(prefabID, out result);
		}

		public static bool TryGetAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : UnityEngine.Object
		{
			var extension = Addressables.customExtension;

			if (extension == null) Instance.SystemLog<SearchedNullAddressablesExtensionException>();

			return extension.TryGetAddressableAsset(assetID, out result);
		}

		public static bool TryGetAddressableScene(string address, out AddressableScene result)
		{
			return Addressables.scenes.BinarySearch(address, out result) >= 0 && result != null;
		}

		public static bool TryGetCustomAddressablesExtension<T>(out T result) where T : ThreadlinkAddressablesExtension
		{
			result = Addressables.customExtension as T;

			bool resultIsValid = result != null;

			if (resultIsValid == false) Instance.SystemLog<AddressablesExtensionNotFoundException>();

			return resultIsValid;
		}
		#endregion
	}
}