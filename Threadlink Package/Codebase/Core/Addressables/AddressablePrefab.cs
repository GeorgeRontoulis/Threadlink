namespace Threadlink.Addressables
{
	using Core;
	using Core.Exceptions;
	using Core.Subsystems.Scribe;
	using Cysharp.Threading.Tasks;
	using System;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;

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

		public GameObject ResultAsGameObject => Handle.IsValid() && Handle.Result != null ? Handle.Result : null;

		private AsyncOperationHandle<GameObject> Handle { get; set; }

		public override void Unload()
		{
			if (Handle.IsValid()) Handle.Release();
		}

		public void LoadSynchronously()
		{
			if (Loaded) return;

			Handle = Addressables.LoadAssetAsync<GameObject>(assetAddress);

			Handle.WaitForCompletion();

			UnloadIfLoadFail();
		}

		public async UniTask LoadAsync()
		{
			if (Loaded) return;

			Handle = Addressables.LoadAssetAsync<GameObject>(assetAddress);

			await Handle.ToUniTask();

			UnloadIfLoadFail();
		}

		private void UnloadIfLoadFail()
		{
			if (Handle.Status.Equals(AsyncOperationStatus.Succeeded) == false)
			{
				Unload();

				throw new AddressableLoadingFailedException(
				Scribe.FromSubsystem<Threadlink>("Failed to load prefab from: ", assetAddress, " !").ToString());
			}
		}
	}
}