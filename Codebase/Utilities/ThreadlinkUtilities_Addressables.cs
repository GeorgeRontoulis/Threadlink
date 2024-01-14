namespace Threadlink.Utilities.Addressables
{
	using Sirenix.OdinInspector;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.ResourceManagement.ResourceProviders;
	using UnityEngine.SceneManagement;
	using Utilities.Collections;
	using Utilities.UnityLogging;

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
		public static void TryRelease(AsyncOperationHandle handle) { if (handle.IsValid()) Addressables.Release(handle); }

		public static bool OperationSucceeded(AsyncOperationHandle operation)
		{
			return operation.Status.Equals(AsyncOperationStatus.Succeeded);
		}

		public static Task UnloadUnusedAssetsAsync()
		{
			var tcs = new TaskCompletionSource<bool>();
			var asyncOp = Resources.UnloadUnusedAssets();

			asyncOp.completed += (operation) => { tcs.SetResult(true); };

			return tcs.Task;
		}
	}

	[Serializable]
	public sealed class AssetGroupAddressPair
	{
#if UNITY_EDITOR
		private bool TargetGroupIsValid => string.IsNullOrEmpty(targetGroup) == false;

		[ValueDropdown("GetAddressableGroupNames")]

		[SerializeField] public string targetGroup = null;
#endif

#if UNITY_EDITOR
		[ValueDropdown("GetAssetPathsInGroup")]
		[ShowIf("TargetGroupIsValid", true)]
#endif
		[SerializeField] public string assetAddress = null;

#if UNITY_EDITOR
		private static List<string> GetAddressableGroupNames()
		{
			return Sirenix.OdinInspector.Editor.AddressablesUtility.GetAddressableGroupNames();
		}

		private List<string> GetAssetPathsInGroup()
		{
			return Sirenix.OdinInspector.Editor.AddressablesUtility.GetAssetPathsInGroup(targetGroup);
		}
#endif
	}

	public abstract class AddressableAsset<T> : Addressable<T> where T : UnityEngine.Object
	{
		public override bool Loaded => Result != null;
		public override T Result => Handle.IsValid() ? Handle.Result : null;
		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<T> Handle { get; set; }

		[SerializeField] AssetGroupAddressPair addressableInfo = new AssetGroupAddressPair();

		public override void Unload() { AddressablesUtilities.TryRelease(Handle); }

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

			if (AddressablesUtilities.OperationSucceeded(Handle) == false)
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
				if (Handle.IsValid() == false) return null;

				return Handle.Result != null ? Handle.Result.GetComponent<T>() : null;
			}
		}
		public override string LinkID => addressableInfo.assetAddress;

		private AsyncOperationHandle<GameObject> Handle { get; set; }

		[SerializeField] AssetGroupAddressPair addressableInfo = new AssetGroupAddressPair();

		public override void Unload() { AddressablesUtilities.TryRelease(Handle); }

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

			if (AddressablesUtilities.OperationSucceeded(Handle) == false)
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
		public override string LinkID => sceneInfo.assetAddress;
		public override SceneInstance Result => Handle.IsValid() ? Handle.Result : new SceneInstance();

		private AsyncOperationHandle<SceneInstance> Handle { get; set; }

		[SerializeField] private AssetGroupAddressPair sceneInfo = new AssetGroupAddressPair();

		public override void Unload() { AddressablesUtilities.TryRelease(Handle); }

		public IEnumerator LoadingCoroutine(LoadSceneMode loadSceneMode)
		{
			if (Loaded) yield break;

			Handle = Addressables.LoadSceneAsync(sceneInfo.assetAddress, loadSceneMode, false);

			while (Handle.IsDone == false) yield return null;

			if (AddressablesUtilities.OperationSucceeded(Handle) == false)
			{
				Unload();

				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to load scene from address: ", sceneInfo.assetAddress);
			}
			else
			{
				yield return Result.ActivateAsync();

				SceneManager.SetActiveScene(Result.Scene);
			}
		}

		public IEnumerator UnloadingCoroutine()
		{
			AsyncOperationHandle<SceneInstance> operation = Addressables.UnloadSceneAsync(Handle, true);

			while (operation.IsDone == false) yield return null;

			if (AddressablesUtilities.OperationSucceeded(operation) == false)
			{
				UnityConsole.Notify(DebugNotificationType.Error,
				"Failed to unload scene from address: ", sceneInfo.assetAddress);
			}
			else Unload();
		}
	}

	#region Commonly used Addressable Classes:

	[Serializable] public sealed class TextAssetAddressable : AddressableAsset<TextAsset> { }
	[Serializable] public sealed class DefaultAssetAddressable : AddressableAsset<UnityEngine.Object> { }

	#endregion
}