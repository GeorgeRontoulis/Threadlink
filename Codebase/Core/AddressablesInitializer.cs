namespace Threadlink.Core
{
	using Cysharp.Threading.Tasks;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.SceneManagement;
	using Utilities.Addressables;

#pragma warning disable UNT0006
#pragma warning disable IDE0051

	internal sealed class AddressablesInitializer : MonoBehaviour
	{
		[SerializeField] private AddressableScene persistentScene = new();

		private async UniTaskVoid Start()
		{
			await Addressables.InitializeAsync();

			await persistentScene.LoadAsync(LoadSceneMode.Single);
		}
	}
}