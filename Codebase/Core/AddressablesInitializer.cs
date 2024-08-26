namespace Threadlink.Core
{
	using System;
	using System.Collections;
	using Systems;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.SceneManagement;
	using Utilities.Addressables;

	internal sealed class AddressablesInitializer : MonoBehaviour
	{
		[SerializeField] private AddressableScene persistentScene = new();

		private IEnumerator Start()
		{
			var handle = Addressables.InitializeAsync(false);

			while (handle.IsDone == false) yield return null;

			const string id = "Addressables Initializer";

			if (AddressablesUtilities.Succeeded(handle))
				Scribe.SystemLog(id, Scribe.InfoNotif, "Successfully initialized Addressables!");
			else
				Scribe.SystemLog<OperationCanceledException>(id, "Addressables failed to initialize! Aborting!");

			handle.TryRelease();

			yield return persistentScene.LoadingCoroutine(LoadSceneMode.Single);
		}
	}
}