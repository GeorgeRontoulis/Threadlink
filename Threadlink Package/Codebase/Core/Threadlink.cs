namespace Threadlink.Core
{
	using Addressables;
	using AYellowpaper.SerializedCollections;
	using Cysharp.Threading.Tasks;
	using Exceptions;
	using Subsystems.Initium;
	using Subsystems.Propagator;
	using Subsystems.Scribe;
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.ResourceProviders;
	using UnityEngine.SceneManagement;
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
		private readonly Dictionary<string, int> ConstantIDsBuffer = new();

		[SerializeField] internal ThreadlinkPreferences preferences = null;

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
			base.Boot();
		}

		internal async UniTask Deploy()
		{
			Boot();

			var subsystemDB = preferences.subsystemDatabase;
			int subSystemCount = subsystemDB.Length;
			var uniTasks = new List<UniTask>(subSystemCount);

			for (int i = 0; i < subSystemCount; i++)
				uniTasks.Add(LoadPrefabAsync<ThreadlinkSubsystem>(subsystemDB[i]));

			await UniTask.WhenAll(uniTasks);

			uniTasks.Clear();

			for (int i = 0; i < subSystemCount; i++)
			{
				if ((subsystemDB[i].Asset as GameObject).TryGetComponent<ThreadlinkSubsystem>(out var subsystem))
				{
					var wovenSystem = Weave(subsystem);

					await Initium.BootAsync(wovenSystem);

					if (wovenSystem is IAddressablesPreloader preloader) uniTasks.Add(preloader.PreloadAssetsAsync());
				}
			}

			await UniTask.WhenAll(uniTasks);

			uniTasks.Clear();
			uniTasks.TrimExcess();

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

		public override T Weave<T>(T original)
		{
			var subsystem = base.Weave(original);
			subsystem.CachedTransform.SetParent(cachedTransform);

			return subsystem;
		}

		#region Miscellaneous:
		public static bool TryGetConstantSingletonID(string singletonName, out int result)
		{
			return Instance.ConstantIDsBuffer.TryGetValue(singletonName, out result);
		}

		public static void RegisterConstantSingletonID(string singletonName, int result)
		{
			Instance.ConstantIDsBuffer.Add(singletonName, result);
		}

		public static async UniTask WaitForFrames(int frameCount)
		{
			for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
		}
		#endregion

		#region Asset Reference/Loading/Unloading API:
		public static async UniTask<T> LoadAssetAsync<T>(ThreadlinkAddressableGroup group, int indexInDB) where T : UnityEngine.Object
		{
			if (ValidateDatabaseRequest(Instance.preferences.assetDatabase, group, indexInDB, out var reference))
			{
				return await LoadAssetAsync<T>(reference);
			}

			return null;
		}

		public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : UnityEngine.Object
		{
			_ = reference.LoadAssetAsync<T>();

			await reference.OperationHandle.ToUniTask();

			return reference.Asset as T;
		}

		public static async UniTask<T> LoadPrefabAsync<T>(ThreadlinkAddressableGroup group, int indexInDB) where T : Component
		{
			if (ValidateDatabaseRequest(Instance.preferences.prefabDatabase, group, indexInDB, out var reference))
			{
				return await LoadPrefabAsync<T>(reference);
			}

			return null;
		}

		public static async UniTask<T> LoadPrefabAsync<T>(AssetReferenceGameObject reference) where T : Component
		{
			_ = reference.LoadAssetAsync();

			await reference.OperationHandle.ToUniTask();

			if ((reference.Asset as GameObject).TryGetComponent<T>(out var component)) return component;
			else
			{
				Scribe.FromSubsystem<Threadlink>("Could not find the requested component of type ",
				typeof(T).Name, " on the loaded prefab!").ToUnityConsole(Scribe.WARN);
				ReleasePrefab(reference);
				return null;
			}
		}

		public static void ReleaseAsset(ThreadlinkAddressableGroup group, int indexInDB)
		{
			if (ValidateDatabaseRequest(Instance.preferences.assetDatabase, group, indexInDB, out var reference) && reference.IsValid())
				reference.ReleaseAsset();
		}

		public static void ReleaseAsset(AssetReference reference)
		{
			if (reference.IsValid()) reference.ReleaseAsset();
		}

		public static void ReleasePrefab(ThreadlinkAddressableGroup group, int indexInDB)
		{
			if (ValidateDatabaseRequest(Instance.preferences.prefabDatabase, group, indexInDB, out var reference) && reference.IsValid())
				reference.ReleaseAsset();
		}

		public static void ReleasePrefab(AssetReferenceGameObject reference)
		{
			if (reference.IsValid()) reference.ReleaseAsset();
		}

		public static async UniTask<SceneInstance> LoadSceneAsync(int sceneReferenceIndex, LoadSceneMode mode)
		{
			if (ValidateDatabaseRequest(sceneReferenceIndex, out var reference))
			{
				_ = reference.LoadSceneAsync(mode);

				return await reference.OperationHandle.Convert<SceneInstance>().ToUniTask();
			}

			throw new AddressableLoadingFailedException();
		}

		public static async UniTask<SceneInstance> UnloadSceneAsync(int sceneReferenceIndex)
		{
			if (ValidateDatabaseRequest(sceneReferenceIndex, out var reference))
			{
				_ = reference.UnLoadScene();

				return await reference.OperationHandle.Convert<SceneInstance>().ToUniTask();
			}

			throw new AddressableLoadingFailedException();
		}

		public static bool TryGetAssetReference(ThreadlinkAddressableGroup group, int indexInDB, out AssetReference result)
		{
			return ValidateDatabaseRequest(Instance.preferences.assetDatabase, group, indexInDB, out result);
		}

		public static bool TryGetPrefabReference(ThreadlinkAddressableGroup group, int indexInDB, out AssetReferenceGameObject result)
		{
			return ValidateDatabaseRequest(Instance.preferences.prefabDatabase, group, indexInDB, out result);
		}

		public static bool TryGetSceneReference(int indexInDB, out SceneAssetReference result)
		{
			return ValidateDatabaseRequest(indexInDB, out result);
		}
		#endregion

		#region Addressable Database Request Validation:
		private static bool ValidateDatabaseRequest(int indexInDB, out SceneAssetReference reference)
		{
			return ValidateAssetReferenceRequest(Instance.preferences.sceneDatabase, indexInDB, out reference);
		}

		private static bool ValidateDatabaseRequest<T>(SerializedDictionary<ThreadlinkAddressableGroup, T[]> database,
		ThreadlinkAddressableGroup group, int indexInDB, out T reference) where T : AssetReference
		{
			var prefs = Instance.preferences;

			if (database.TryGetValue(group, out var assetRefCollection))
			{
				return ValidateAssetReferenceRequest(assetRefCollection, indexInDB, out reference);
			}
			else Scribe.FromSubsystem<Threadlink>("The requested asset group does not exist in the database!").
			ToUnityConsole(prefs, Scribe.WARN);

			reference = null;
			return false;
		}

		private static bool ValidateAssetReferenceRequest<T>(T[] assetRefCollection, int indexInDB, out T reference) where T : AssetReference
		{
			var prefs = Instance.preferences;

			reference = null;

			if (!indexInDB.IsWithinBoundsOf(assetRefCollection))
			{
				Scribe.FromSubsystem<Threadlink>("The Asset Reference Index ", indexInDB, " is invalid!").
				ToUnityConsole(prefs, Scribe.WARN);
				return false;
			}

			var assetReference = assetRefCollection[indexInDB];

			if (assetReference == null)
			{
				Scribe.FromSubsystem<Threadlink>(assetReference, " at index ", indexInDB, " is NULL!").
				ToUnityConsole(prefs, Scribe.WARN);
				return false;
			}
			else if (!assetReference.RuntimeKeyIsValid())
			{
				Scribe.FromSubsystem<Threadlink>("RuntimeKey of ", assetReference, ", ", assetReference.RuntimeKey, " is invalid!").
				ToUnityConsole(prefs, Scribe.WARN);
				return false;
			}

			reference = assetReference;
			return true;
		}
		#endregion
	}
}