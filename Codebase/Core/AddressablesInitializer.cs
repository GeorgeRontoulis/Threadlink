namespace Threadlink.Core
{
	using System.Collections;
	using Systems;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.AddressableAssets.ResourceLocators;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.SceneManagement;
	using Utilities.Addressables;
	using Utilities.UnityLogging;

	internal sealed class AddressablesInitializer : MonoBehaviour
	{
		[SerializeField] private AddressableScene persistentScene = new();

		private IEnumerator Start()
		{
			AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync(false);

			while (handle.IsDone == false) yield return null;

			bool initialized = AddressablesUtilities.OperationSucceeded(handle);
			DebugNotificationType notifType = initialized ? DebugNotificationType.Info : DebugNotificationType.Error;
			string notification = initialized ? "Successfully initialized Addressables!" : "Addressables failed to initialize! Aborting!";

			Scribe.SystemLog("Addressables Initializer", notifType, notification);

			AddressablesUtilities.ReleaseIfValid(handle);

			yield return persistentScene.LoadingCoroutine(LoadSceneMode.Single);
		}
	}
}