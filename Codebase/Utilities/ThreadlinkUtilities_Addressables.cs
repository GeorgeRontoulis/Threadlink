namespace Threadlink.Utilities.Addressables
{
	using System;
	using System.Collections;
	using System.Threading.Tasks;
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
	public sealed class AssetGroupAddressPair
	{
#if UNITY_EDITOR
		[AddressableAssetButton]
#endif
		[SerializeField] public string assetAddress = string.Empty;
	}

	public abstract class AddressableAsset<T> : Addressable<T> where T : UnityEngine.Object
	{
		public override bool Loaded => Result != null;
		public override T Result => Handle.IsValid() ? Handle.Result : null;
		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<T> Handle { get; set; }

		[SerializeField] AssetGroupAddressPair addressableInfo = new AssetGroupAddressPair();

		public override void Unload() { Handle.TryRelease(); }

		public async Task<T> LoadAsync()
		{
			Handle = Addressables.LoadAssetAsync<T>(addressableInfo.assetAddress);

			await Handle.Task;

			return Result;
		}

		public IEnumerator LoadingCoroutine()
		{
			Handle = Addressables.LoadAssetAsync<T>(addressableInfo.assetAddress);

			while (Handle.IsDone == false) yield return null;

			if (Handle.Succeeded() == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to load asset from address: ", addressableInfo.assetAddress);
			}
		}
	}

	public abstract class AddressablePrefab<T> : Addressable<T> where T : Component
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
		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<GameObject> Handle { get; set; }

		[SerializeField] AssetGroupAddressPair addressableInfo = new();

		public override void Unload() { Handle.TryRelease(); }

		public async Task<T> LoadAsync()
		{
			if (Loaded) return Result;

			Handle = Addressables.LoadAssetAsync<GameObject>(addressableInfo.assetAddress);

			await Handle.Task;

			return Result;
		}

		public IEnumerator LoadingCoroutine()
		{
			if (Loaded) yield break;

			Handle = Addressables.LoadAssetAsync<GameObject>(addressableInfo.assetAddress);

			while (Handle.IsDone == false) yield return null;

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
		public override SceneInstance Result => Handle.IsValid() ? Handle.Result : new SceneInstance();

		private AsyncOperationHandle<SceneInstance> Handle { get; set; }

		[SerializeField] private AssetGroupAddressPair addressableReference = new();

		public override void Unload() { Handle.TryRelease(); }

		public IEnumerator LoadingCoroutine(LoadSceneMode loadSceneMode)
		{
			if (Loaded) yield break;

			Handle = Addressables.LoadSceneAsync(addressableReference.assetAddress, loadSceneMode, false);

			while (Handle.IsDone == false) yield return null;

			if (Handle.Succeeded() == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to load scene from address: ", addressableReference.assetAddress);
			}
			else
			{
				yield return Result.ActivateAsync();

				if (SceneManager.SetActiveScene(Result.Scene) == false)
				{
					UnityConsole.Notify(DebugNotificationType.Error,
					"Scene Manager failed to activate the requested scene, because it is not loaded! This should never happen!");
				}
			}
		}

		public IEnumerator UnloadingCoroutine()
		{
			var operation = Addressables.UnloadSceneAsync(Handle, true);

			while (operation.IsDone == false) yield return null;

			if (operation.Succeeded() == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to unload scene from address: ", addressableReference.assetAddress);
			}
			else Unload();
		}
	}

	#region Commonly used Addressable Classes:

	[Serializable] public sealed class TextAssetAddressable : AddressableAsset<TextAsset> { }
	[Serializable] public sealed class DefaultAssetAddressable : AddressableAsset<UnityEngine.Object> { }

	#endregion
}