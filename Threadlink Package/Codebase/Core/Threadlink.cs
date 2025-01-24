namespace Threadlink.Core
{
	using Addressables;
	using Addressables.Extensions;
	using Cysharp.Threading.Tasks;
	using Exceptions;
	using Subsystems.Initium;
	using Subsystems.Propagator;
	using Subsystems.Scribe;
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using Utilities.Collections;

#if UNITY_EDITOR
#if THREADLINK_INSPECTOR
	using Editor.Attributes;
#elif ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#endif

	internal static class ThreadlinkDeployment
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static async UniTaskVoid DeployCore()
		{
			await Addressables.InitializeAsync();

			var handle = Addressables.LoadAssetAsync<GameObject>("Assets/Threadlink/Threadlink Package/Prefabs/Threadlink.prefab");

			await handle.ToUniTask();

			var coreSystemAsset = handle.Result;

			if (coreSystemAsset.TryGetComponent(out Threadlink systemComponent) && systemComponent.preferences != null)
			{
				if (systemComponent.preferences.coreDeployment == 0)
				{
					var threadlinkGO = UnityEngine.Object.Instantiate(coreSystemAsset);
					var threadlinkInstance = threadlinkGO.GetComponent<Threadlink>();

					UnityEngine.Object.DontDestroyOnLoad(threadlinkGO);

					threadlinkInstance.name = nameof(Threadlink);
					await threadlinkInstance.Deploy();

					var collection = UnityEngine.Object.FindAnyObjectByType<InitializableCollection>(FindObjectsInactive.Exclude);

					if (collection != null) await Initium.BootAndInitCollectionAsync(collection);
					else Scribe.FromSubsystem<Threadlink>("No initializable collection found!").ToUnityConsole(Scribe.WARN);
				}
			}
			else throw new NullReferenceException(Scribe.FromSubsystem<Threadlink>("Component is NULL or Preferences have not been assigned!").ToString());
		}
	}

	/// <summary>
	/// The Core Game System. Controls all aspects of the runtime
	/// and manages Subsystems and Entities at the lowest level.
	/// </summary>
	public sealed class Threadlink : UnityWeaver<Threadlink, ThreadlinkSubsystem>
	{
#if UNITY_EDITOR && ODIN_INSPECTOR
		[ShowInInspector]
		[ReadOnly]
#endif
		private readonly Dictionary<string, Ulid> ConstantIDsBuffer = new();

		[Space(10)]

		[SerializeField] internal AddressableScene[] runtimeScenes = new AddressableScene[0];

		[Space(10)]

		[SerializeField] internal ThreadlinkPreferences preferences = null;
		[SerializeField] internal ThreadlinkAddressablesExtension addressablesExtension = null;

		#region Main Lifecycle API:
		public static void ShutDown() => Instance.Discard();

		public override void Discard()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
		}

		public override void Boot()
		{
			ConstantIDsBuffer.Clear();
			if (addressablesExtension != null) addressablesExtension.Boot();
			base.Boot();
		}

		internal async UniTask Deploy()
		{
			Boot();

			var preferencesArray = preferences.nativeSubSystems;
			int subSystemCount = preferencesArray.Length - 1; //Exclude core system.
			var subsystemAddressables = new List<AddressablePrefab<ThreadlinkSubsystem>>(subSystemCount);

			subsystemAddressables.PopulateWithNewInstances(subSystemCount);

			for (int i = 0; i < subSystemCount; i++) subsystemAddressables[i].assetAddress = preferencesArray[i + 1]; //Exclude core system.

			var uniTasks = new UniTask[subSystemCount];

			for (int i = 0; i < subSystemCount; i++) uniTasks[i] = subsystemAddressables[i].LoadAsync();

			await UniTask.WhenAll(uniTasks);

			for (int i = 0; i < subSystemCount; i++)
			{
				var wovenSystem = Weave(subsystemAddressables[i].Result);

				await Initium.BootAsync(wovenSystem);

				if (wovenSystem is IAddressablesPreloader preloader) await preloader.PreloadAssetsAsync();
			}

			subsystemAddressables.Clear();
			subsystemAddressables.TrimExcess();

			foreach (var subSystem in Registry.Values)
			{
				if (subSystem is IInitializable initializable) await Initium.InitializeAsync(initializable);
			}

			enabled = true;
			Scribe.FromSubsystem<Threadlink>("Core successfully deployed. All Subsystems operational.").ToUnityConsole(this);
		}
		#endregion

		#region Unity Update Messages:
		private void Update() => Propagator.Publish(PropagatorEvents.OnUpdate);
		private void FixedUpdate() => Propagator.Publish(PropagatorEvents.OnFixedUpdate);
		private void LateUpdate() => Propagator.Publish(PropagatorEvents.OnLateUpdate);
		#endregion

		public override ThreadlinkSubsystem Weave(ThreadlinkSubsystem original)
		{
			var system = base.Weave(original);
			system.CachedTransform.SetParent(cachedTransform);

			return system;
		}

		public static bool TryGetConstantSingletonID(string singletonName, out Ulid result)
		{
			return Instance.ConstantIDsBuffer.TryGetValue(singletonName, out result);
		}

		public static void RegisterConstantSingletonID(string singletonName, Ulid result)
		{
			Instance.ConstantIDsBuffer.Add(singletonName, result);
		}

		public static async UniTask WaitForFrames(int frameCount)
		{
			for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
		}

		#region Addressables Methods
		public static bool TryGetAddressablePrefab<PrefabType>(string prefabID, out AddressablePrefab<PrefabType> result)
		where PrefabType : Component
		{
			var extension = Instance.addressablesExtension;

			if (extension == null)
			{
				throw new NullAddressablesExtensionException(
				Scribe.FromSubsystem<Threadlink>("No ", nameof(ThreadlinkAddressablesExtension), " has been specified!").ToString());
			}

			return extension.TryGetAddressablePrefab(prefabID, out result);
		}

		public static bool TryGetAddressableAsset<AssetType>(string assetID, out AddressableAsset<AssetType> result)
		where AssetType : UnityEngine.Object
		{
			var extension = Instance.addressablesExtension;

			if (extension == null)
			{
				throw new NullAddressablesExtensionException(
				Scribe.FromSubsystem<Threadlink>("No ", nameof(ThreadlinkAddressablesExtension), " has been specified!").ToString());
			}

			return extension.TryGetAddressableAsset(assetID, out result);
		}

		public static bool TryGetAddressableScene(string address, out AddressableScene result)
		{
			bool Matches(AddressableScene scene) => scene.assetAddress.Equals(address);

			return Instance.runtimeScenes.BruteForceSearch(Matches, out result) && result != null;
		}

		public static T GetCustomAddressablesExtension<T>() where T : ThreadlinkAddressablesExtension
		{
			var result = (Instance.addressablesExtension is T customExtension ? customExtension : null)
			?? throw new NullAddressablesExtensionException(Scribe.FromSubsystem<Threadlink>("No ", nameof(T), " has been specified!").ToString());

			return result;
		}
		#endregion
	}
}