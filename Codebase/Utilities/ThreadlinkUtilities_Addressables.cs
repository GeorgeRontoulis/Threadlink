namespace Threadlink.Utilities.Addressables
{
	using Collections;
	using Cysharp.Threading.Tasks;
	using MassTransit;
	using System;
	using Systems;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.ResourceManagement.ResourceProviders;
	using UnityEngine.SceneManagement;
	using UnityLogging;

#if UNITY_EDITOR
	using Editor.Attributes;
#endif

	public interface IAssetPreloader
	{
		public UniTask PreloadAssetsAsync();
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

	public abstract class Addressable : IIdentifiable
	{
		public virtual string LinkID => assetAddress;
		public NewId InstanceID
		{
			get => NewId.Empty;
			set => _ = NewId.Empty;
		}

		public abstract bool Loaded { get; }

#if UNITY_EDITOR
		[AddressableAssetButton]
#endif
		public string assetAddress = string.Empty;

		public abstract void Unload();
	}

	public abstract class Addressable<T> : Addressable
	{
		public abstract T Result { get; }
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

		private AsyncOperationHandle<T> Handle { get; set; }

		public override void Unload() { Handle.TryRelease(); }

		public async UniTask LoadAsync()
		{
			Handle = Addressables.LoadAssetAsync<T>(assetAddress);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error, context: null,
				"Failed to load asset from address: ", assetAddress);
				this.LogException<AddressableLoadingFailedException>();
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

		private AsyncOperationHandle<GameObject> Handle { get; set; }

		public override void Unload() { Handle.TryRelease(); }

		public void LoadSynchronously()
		{
			if (Loaded) return;

			Handle = Addressables.LoadAssetAsync<GameObject>(assetAddress);

			Handle.WaitForCompletion();

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error, context: null,
				"Failed to load prefab from address: ", assetAddress);
				this.LogException<AddressableLoadingFailedException>();
			}
		}

		public async UniTask LoadAsync()
		{
			if (Loaded) return;

			Handle = Addressables.LoadAssetAsync<GameObject>(assetAddress);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error, context: null,
				"Failed to load prefab from address: ", assetAddress);
				this.LogException<AddressableLoadingFailedException>();
			}
		}
	}

	[Serializable]
	public sealed class AddressableScene : Addressable<SceneInstance>
	{
		public override bool Loaded => Handle.IsValid() && Handle.Result.Scene != null;
		public override SceneInstance Result => Handle.IsValid() ? Handle.Result : default;

		private AsyncOperationHandle<SceneInstance> Handle { get; set; }

		public override void Unload() { Handle.TryRelease(); }

		public async UniTask LoadAsync(LoadSceneMode loadSceneMode)
		{
			if (Loaded) return;

			Handle = Addressables.LoadSceneAsync(assetAddress, loadSceneMode, false);

			await Handle.ToUniTask();

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error, context: null,
				"Failed to load scene from address: ", assetAddress);
				this.LogException<AddressableLoadingFailedException>();
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
				UnityConsole.Notify(DebugNotificationType.Error, context: null,
				"Failed to unload scene from address: ", assetAddress);
				this.LogException<AddressableLoadingFailedException>();
			}
			else Unload();
		}
	}
}