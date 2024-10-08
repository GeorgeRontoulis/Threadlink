namespace Threadlink.Systems.Nexus
{
	using Aura;
	using Core;
	using Cysharp.Threading.Tasks;
	using Dextra;
	using Extensions.Nexus;
	using System;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.Events;

	/// <summary>
	/// System responsible for scene and player loading during Threadlink's runtime.
	/// </summary>
	public sealed class Nexus : UnitySystem<Nexus, LinkableBehaviour>
	{
		public static PlayerLoaderExtension CustomPlayerLoader => Instance.customPlayerLoader;

		public static VoidEvent OnBeforeSceneUnload => Instance.onBeforeSceneUnload;
		public static VoidEvent OnAfterSceneUnload => Instance.onAfterSceneUnload;
		public static VoidEvent OnSceneFinishedLoading => Instance.onSceneFinishedLoading;
		public static VoidGenericEvent<SceneEntry> OnAfterSceneLoad => Instance.onAfterSceneLoad;
		public static VoidGenericEvent<Vector3> OnPlayerPlacementAfterSceneLoad => Instance.onPlayerPlacementAfterSceneLoad;
		public static GenericEvent<SceneEntry> OnActiveSceneRetrieval => Instance.onActiveSceneRetrieval;
		public static GenericEvent<UniTask> OnDisplayFader => Instance.onDisplayFader;
		public static GenericEvent<UniTask> OnDisplayLoadingScreen => Instance.onDisplayLoadingScreen;
		public static GenericEvent<UniTask> OnHideFader => Instance.onHideFader;
		public static GenericEvent<UniTask> OnHideLoadingScreen => Instance.onHideLoadingScreen;

		[SerializeField] private SceneEntry startingSceneEntry = null;
		[SerializeField] private int startingEntranceIndex = -1;

		[Space(10)]

		[SerializeField] private PlayerLoaderExtension customPlayerLoader = null;

		[NonSerialized] private VoidEvent onBeforeSceneUnload = new();
		[NonSerialized] private VoidEvent onAfterSceneUnload = new();
		[NonSerialized] private VoidEvent onSceneFinishedLoading = new();
		[NonSerialized] private VoidGenericEvent<SceneEntry> onAfterSceneLoad = new();
		[NonSerialized] private VoidGenericEvent<Vector3> onPlayerPlacementAfterSceneLoad = new();
		[NonSerialized] private GenericEvent<SceneEntry> onActiveSceneRetrieval = new();
		[NonSerialized] private GenericEvent<UniTask> onDisplayFader = new();
		[NonSerialized] private GenericEvent<UniTask> onDisplayLoadingScreen = new();
		[NonSerialized] private GenericEvent<UniTask> onHideFader = new();
		[NonSerialized] private GenericEvent<UniTask> onHideLoadingScreen = new();

		public override VoidOutput Discard(VoidInput _ = default)
		{
			if (customPlayerLoader != null) customPlayerLoader.Discard();
			onSceneFinishedLoading.Discard();
			onBeforeSceneUnload.Discard();
			onAfterSceneUnload.Discard();
			onAfterSceneLoad.Discard();
			onActiveSceneRetrieval.Discard();
			onDisplayFader.Discard();
			onDisplayLoadingScreen.Discard();
			onHideFader.Discard();
			onHideLoadingScreen.Discard();
			onPlayerPlacementAfterSceneLoad.Discard();

			onSceneFinishedLoading = null;
			onPlayerPlacementAfterSceneLoad = null;
			onBeforeSceneUnload = null;
			onAfterSceneUnload = null;
			onAfterSceneLoad = null;
			onActiveSceneRetrieval = null;
			onDisplayFader = null;
			onDisplayLoadingScreen = null;
			onHideFader = null;
			onHideLoadingScreen = null;
			customPlayerLoader = null;
			startingSceneEntry = null;
			return base.Discard(_);
		}

		public override void Boot()
		{
			base.Boot();

			if (customPlayerLoader != null)
			{
				customPlayerLoader = customPlayerLoader.Clone();
				customPlayerLoader.Boot();
			}
		}

		public override void Initialize()
		{
			if (customPlayerLoader != null) customPlayerLoader.Initialize();

			if (startingSceneEntry != null) LoadSceneAsync(startingSceneEntry, startingEntranceIndex).Forget();
		}

		public static async UniTask LoadSceneAsync(SceneEntry sceneEntry, int entranceIndex = -1)
		{
			static SceneEntry RetrieveActiveScene() { return Instance.onActiveSceneRetrieval?.Invoke(); }

			//Disable player input.
			Dextra.CurrentInputMode = Dextra.InputMode.Invalid;

			//Pause the game.
			Chronos.RawTimeScale = 0;

			//Fade the Global Game Volume to 0.
			var volumeTask = Aura.AsyncFadeAudioListenerVolumeTo(0f);

			//Display the Fader and wait until it is fully visible.
			var faderTask = Instance.onDisplayFader.Invoke();

			await UniTask.WhenAll(volumeTask, faderTask);

			//Display the Loading Screen and wait until it is fully visible.
			await Instance.onDisplayLoadingScreen.Invoke();
			//Hide the Fader and wait until it is fully hidden.
			await Instance.onHideFader.Invoke();

			var playerLoadingAction = sceneEntry.playerLoadingAction;
			var playerLoader = Instance.customPlayerLoader;

			if (playerLoader != null && playerLoader.PlayerIsLoaded && playerLoadingAction.Equals(PlayerLoadingAction.Unload)) playerLoader.Unload();

			var activeScene = RetrieveActiveScene();

			if (activeScene != null)
			{
				await activeScene.OnBeforeUnloadedAsync();
				Instance.onBeforeSceneUnload?.Invoke();

				await activeScene.AddressableScene.UnloadAsync();

				Instance.onAfterSceneUnload?.Invoke();
			}

			await sceneEntry.AddressableScene.LoadAsync(sceneEntry.loadingMode);

			Instance.onAfterSceneLoad?.Invoke(sceneEntry);
			activeScene = RetrieveActiveScene();

			if (playerLoader != null && playerLoader.PlayerIsLoaded == false && playerLoadingAction.Equals(PlayerLoadingAction.Load))
			{
				await playerLoader.LoadPlayerAndDependeciesAsync();
			}

			if (playerLoader.PlayerIsLoaded)
			{
				var spawnPoints = sceneEntry.playerSpawnPoints;
				Instance.onPlayerPlacementAfterSceneLoad?.Invoke(entranceIndex.IsWithinBoundsOf(spawnPoints) ?
				spawnPoints[entranceIndex] : default);
			}

			string sceneName = activeScene != null ?
			activeScene.AddressableScene.Result.Scene.name
			:
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

			if (Initium.GetSceneInitCollection(sceneName, out var initCollection))
				await Initium.BootAndInitCollection(initCollection);

			await sceneEntry.OnFinishedLoadingAsync();

			Instance.onSceneFinishedLoading.Invoke();
			Chronos.RawTimeScale = 1;

			await Instance.onDisplayFader.Invoke();

			await Instance.onHideLoadingScreen.Invoke();

			volumeTask = Aura.AsyncFadeAudioListenerVolumeTo(1f);
			faderTask = Instance.onHideFader.Invoke();

			//Re-enable player input, if appropriate.
			Dextra.CurrentInputMode = playerLoader.PlayerIsLoaded ? Dextra.InputMode.Player : Dextra.InputMode.UI;

			await UniTask.WhenAll(volumeTask, faderTask);
		}
	}
}