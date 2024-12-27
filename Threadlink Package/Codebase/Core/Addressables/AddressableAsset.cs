namespace Threadlink.Addressables
{
	using Core;
	using Core.Exceptions;
	using Core.Subsystems.Scribe;
	using Cysharp.Threading.Tasks;
	using System;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;

	[Serializable]
	public class AddressableAsset<T> : Addressable<T> where T : UnityEngine.Object
	{
		public override bool Loaded => Result != null;
		public override T Result => Handle.IsValid() ? Handle.Result : null;

		private AsyncOperationHandle<T> Handle { get; set; }

		public override void Unload()
		{
			if (Handle.IsValid()) Handle.Release();
		}

		public async UniTask LoadAsync()
		{
			Handle = Addressables.LoadAssetAsync<T>(assetAddress);

			await Handle.ToUniTask();

			if (Handle.Status.Equals(AsyncOperationStatus.Succeeded) == false)
			{
				Unload();

				throw new AddressableLoadingFailedException(
				Scribe.FromSubsystem<Threadlink>("Failed to load asset from: ", assetAddress, " !").ToString());
			}
		}
	}
}