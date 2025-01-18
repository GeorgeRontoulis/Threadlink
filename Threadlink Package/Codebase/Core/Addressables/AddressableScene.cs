namespace Threadlink.Addressables
{
	using Core;
	using Core.Exceptions;
	using Core.Subsystems.Scribe;
	using Cysharp.Threading.Tasks;
	using System;
	using UnityEngine.AddressableAssets;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.ResourceManagement.ResourceProviders;
	using UnityEngine.SceneManagement;

	[Serializable]
	public sealed class AddressableScene : Addressable<SceneInstance>
	{
		public override bool Loaded => Handle.IsValid() && Handle.Result.Scene != null;
		public override SceneInstance Result => Handle.IsValid() ? Handle.Result : default;

		private AsyncOperationHandle<SceneInstance> Handle { get; set; }

		public override void Unload()
		{
			if (Handle.IsValid()) Handle.Release();
		}

		public async UniTask LoadAsync(LoadSceneMode loadSceneMode)
		{
			if (Loaded) return;

			Handle = Addressables.LoadSceneAsync(assetAddress, loadSceneMode, false, 100, 0);

			await Handle.ToUniTask();

			if (Handle.Status.Equals(AsyncOperationStatus.Succeeded) == false)
			{
				Unload();
				Throw("load");
			}
			else
			{
				var operation = Result.ActivateAsync();

				while (operation.isDone == false) await Threadlink.WaitForFrames(1);
			}
		}

		public async UniTask UnloadAsync()
		{
			var operation = Addressables.UnloadSceneAsync(Handle, true);

			await operation.ToUniTask();

			if (Handle.Status.Equals(AsyncOperationStatus.Succeeded) == false)
				Throw("unload");
			else
				Unload();
		}

		private void Throw(string verb)
		{
			throw new AddressableLoadingFailedException(
			Scribe.FromSubsystem<Threadlink>("Failed to ", verb, " scene from: ", assetAddress, " !").ToString());
		}
	}
}