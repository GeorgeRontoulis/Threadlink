namespace Threadlink.Core
{
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

			bool initialized = AddressablesUtilities.Succeeded(handle);
			var notifType = initialized ? Scribe.InfoNotif : Scribe.ErrorNotif;
			string notification = initialized ? "Successfully initialized Addressables!" : "Addressables failed to initialize! Aborting!";

			Scribe.SystemLog("Addressables Initializer", notifType, notification);

			handle.TryRelease();

			yield return persistentScene.LoadingCoroutine(LoadSceneMode.Single);
		}
	}
}