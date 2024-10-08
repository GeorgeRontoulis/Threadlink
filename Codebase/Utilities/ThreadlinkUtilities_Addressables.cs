namespace Threadlink.Utilities.Addressables
{
	using System;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.ResourceManagement.ResourceProviders;
	using UnityEngine.SceneManagement;
	using Collections;
#if UNITY_EDITOR
	using Editor.Attributes;
#endif
	using UnityLogging;
	using Cysharp.Threading.Tasks;

	public interface IAssetPreloader
	{
		public UniTask PreloadAssetsAsync();
	}

	public abstract class Addressable : IIdentifiable
	{
		public abstract string LinkID { get; }
		public abstract bool Loaded { get; }

		public abstract void Unload();
	}

	public abstract class Addressable<T> : Addressable
	{
		public abstract T Result { get; }
	}

	public static class AddressablesUtilities
	{
		public static bool TryRelease<T>(this AsyncOperationHandle<T> handle)
		{
			if (handle.IsValid())
			{
				Addressables.Release(handle);
				return true;
			}

			return false;
		}

		public static bool Succeeded<T>(this AsyncOperationHandle<T> operation)
		{
			return operation.Status.Equals(AsyncOperationStatus.Succeeded);
		}
	}

	[Serializable]
	public sealed class AddressablePointer
	{
#if UNITY_EDITOR
		[AddressableAssetButton]
#endif
		public string assetAddress = string.Empty;
	}

	[Serializable]
	public class AddressableAsset<T> : Addressable<T> where T : UnityEngine.Object
	{
		public override bool Loaded => Result != null;
		public override T Result => Handle.IsValid() ? Handle.Result : null;
		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<T> Handle { get; set; }

		[SerializeField] AddressablePointer addressableInfo = new();

		public override void Unload() { Handle.TryRelease(); }

		public async UniTask LoadAsync()
		{
			Handle = Addressables.LoadAssetAsync<T>(addressableInfo.assetAddress);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to load asset from address: ", addressableInfo.assetAddress);
			}
		}
	}

	[Serializable]
	public class AddressablePrefab<T> : Addressable<T> where T : Component
	{
		public override bool Loaded => Result != null;
		public override T Result
		{
			get
			{
				if (Handle.IsValid() == false || Handle.Result == null) return null;

				Handle.Result.TryGetComponent<T>(out var result);

				return result;
			}
		}

		public GameObject ResultAsGameObject => Handle.IsValid() == false || Handle.Result == null ? null : Handle.Result;

		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<GameObject> Handle { get; set; }

		[SerializeField] AddressablePointer addressableInfo = new();

		public override void Unload() { Handle.TryRelease(); }

		public async UniTask LoadAsync()
		{
			if (Loaded) return;

			Handle = Addressables.LoadAssetAsync<GameObject>(addressableInfo.assetAddress);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to load prefab from address: ", addressableInfo.assetAddress);
			}
		}
	}

	[Serializable]
	public sealed class AddressableScene : Addressable<SceneInstance>
	{
		public override bool Loaded => Handle.IsValid() && Handle.Result.Scene != null;
		public override string LinkID => addressableReference.assetAddress;
		public override SceneInstance Result => Handle.IsValid() ? Handle.Result : default;

		private AsyncOperationHandle<SceneInstance> Handle { get; set; }

		[SerializeField] private AddressablePointer addressableReference = new();

		public override void Unload() { Handle.TryRelease(); }

		public async UniTask LoadAsync(LoadSceneMode loadSceneMode)
		{
			if (Loaded) return;

			Handle = Addressables.LoadSceneAsync(addressableReference.assetAddress, loadSceneMode, false);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error, "Failed to load scene from address: ''", addressableReference.assetAddress, "''");
			}
			else
			{
				await Result.ActivateAsync();

				SceneManager.SetActiveScene(Result.Scene);
			}
		}

		public async UniTask UnloadAsync()
		{
			var operation = Addressables.UnloadSceneAsync(Handle, true);

			await operation.ToUniTask();

			if (operation.Succeeded() == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to unload scene from address: ", addressableReference.assetAddress);
			}
			else Unload();
		}
	}
}