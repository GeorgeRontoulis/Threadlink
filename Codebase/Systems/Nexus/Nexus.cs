namespace Threadlink.Systems.Nexus
{
	using System.Collections;
	using Threadlink.Core;
	using Threadlink.Extensions.Nexus;
	using Threadlink.Systems.Initium;
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// System responsible for scene and player loading during Threadlink's runtime.
	/// </summary>
	public sealed class Nexus : ThreadlinkSystem<Nexus, LinkableBehaviour>
	{
		public static BasePlayerLoaderExtension CustomPlayerLoader => Instance.customPlayerLoader;

		public static VoidEvent OnBeforeSceneUnload => Instance.onBeforeSceneUnload;
		public static VoidEvent OnAfterSceneUnload => Instance.onAfterSceneUnload;
		public static VoidGenericEvent<SceneEntry> OnAfterSceneLoad => Instance.onAfterSceneLoad;
		public static GenericEvent<SceneEntry> OnActiveSceneRetrieval => Instance.onActiveSceneRetrieval;
		public static GenericEvent<IEnumerator> OnDisplayFader => Instance.onDisplayFader;
		public static GenericEvent<IEnumerator> OnDisplayLoadingScreen => Instance.onDisplayLoadingScreen;
		public static GenericEvent<IEnumerator> OnHideFader => Instance.onHideFader;
		public static GenericEvent<IEnumerator> OnHideLoadingScreen => Instance.onHideLoadingScreen;

		[SerializeField] private SceneEntry startingSceneEntry = null;

		[Space(10)]

		[SerializeField] private BasePlayerLoaderExtension customPlayerLoader = null;

		private VoidEvent onBeforeSceneUnload = new();
		private VoidEvent onAfterSceneUnload = new();
		private VoidGenericEvent<SceneEntry> onAfterSceneLoad = new();
		private GenericEvent<SceneEntry> onActiveSceneRetrieval = new();
		private GenericEvent<IEnumerator> onDisplayFader = new();
		private GenericEvent<IEnumerator> onDisplayLoadingScreen = new();
		private GenericEvent<IEnumerator> onHideFader = new();
		private GenericEvent<IEnumerator> onHideLoadingScreen = new();

		public override void Discard()
		{
			onBeforeSceneUnload.Discard();
			onAfterSceneUnload.Discard();
			onAfterSceneLoad.Discard();
			onActiveSceneRetrieval.Discard();
			onDisplayFader.Discard();
			onDisplayLoadingScreen.Discard();
			onHideFader.Discard();
			onHideLoadingScreen.Discard();

			onBeforeSceneUnload = null;
			onAfterSceneUnload = null;
			onAfterSceneLoad = null;
			onActiveSceneRetrieval = null;
			onDisplayFader = null;
			onDisplayLoadingScreen = null;
			onHideFader = null;
			onHideLoadingScreen = null;
			base.Discard();
		}

		public override void Boot()
		{
			Instance = this;

			if (customPlayerLoader != null)
			{
				customPlayerLoader = Instantiate(customPlayerLoader);
				customPlayerLoader.Boot();
			}

			base.Boot();
		}

		public override void Initialize()
		{
			if (customPlayerLoader != null) customPlayerLoader.Initialize();
			if (startingSceneEntry != null) Threadlink.LaunchCoroutine(SceneLoadingCoroutine(startingSceneEntry), false);
		}

		public static IEnumerator SceneLoadingCoroutine(SceneEntry sceneEntry)
		{
			static SceneEntry RetrieveActiveScene() { return Instance.onActiveSceneRetrieval.Invoke(); }

			//Pause the game.
			Chronos.RawTimeScale = 0;
			//Display the Fader and wait until it is fully visible.
			yield return Instance.onDisplayFader.Invoke();
			//Display the Loading Screen and wait until it is fully visible.
			yield return Instance.onDisplayLoadingScreen.Invoke();
			//Hide the Fader and wait until it is fully hidden.
			yield return Instance.onHideFader.Invoke();

			var playerLoadingAction = sceneEntry.playerLoadingAction;
			var playerLoader = Instance.customPlayerLoader;

			if (playerLoader != null && playerLoader.PlayerIsLoaded && playerLoadingAction.Equals(PlayerLoadingAction.Unload)) playerLoader.Unload();

			var activeScene = RetrieveActiveScene();

			if (activeScene != null)
			{
				Instance.onBeforeSceneUnload.Invoke();

				yield return activeScene.AddressableScene.UnloadingCoroutine();

				Instance.onAfterSceneUnload.Invoke();
			}

			yield return sceneEntry.AddressableScene.LoadingCoroutine(sceneEntry.loadingMode);

			Instance.onAfterSceneLoad.Invoke(sceneEntry);
			activeScene = RetrieveActiveScene();

			if (playerLoader != null && playerLoader.PlayerIsLoaded == false && playerLoadingAction.Equals(PlayerLoadingAction.Load))
			{
				yield return playerLoader.LoadingCoroutine();
			}

			string sceneName = activeScene != null ?
			activeScene.AddressableScene.Result.Scene.name
			:
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

			var initCollection = Initium.GetSceneInitCollection(sceneName);
			if (initCollection != null) yield return Initium.BootAndInitCollection(initCollection);

			yield return sceneEntry.PostLoadingCoroutine();

			yield return Instance.onDisplayFader.Invoke();

			yield return Instance.onHideLoadingScreen.Invoke();

			yield return Instance.onHideFader.Invoke();

			Chronos.RawTimeScale = 1;
		}
	}
}