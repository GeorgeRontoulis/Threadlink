namespace Threadlink.Core.Subsystems.Nexus
{
	using Aura;
	using Chronos;
	using Core;
	using Cysharp.Threading.Tasks;
	using Dextra;
	using Initium;
	using Propagator;
	using UnityEngine;
	using Utilities.Collections;

	/// <summary>
	/// System responsible for scene and player loading during Threadlink's runtime.
	/// </summary>
	public sealed class Nexus : ThreadlinkSubsystem<Nexus>
	{
		internal enum PlayerLoadingAction : byte { None, Unload, Load }

		[SerializeField] private SceneEntry startingSceneEntry = null;
		[SerializeField] private int startingEntranceIndex = -1;

		[Space(10)]

		[SerializeField] private bool loadStartingSceneOnBootup = false;

		#region Threadlink Lifecycle API:
		public override void Discard()
		{
			startingSceneEntry = null;
			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();

			if (loadStartingSceneOnBootup) LoadStartingScene();
		}
		#endregion

		public static void LoadStartingScene()
		{
			var entry = Instance.startingSceneEntry;

			if (entry != null) LoadSceneAsync(entry, Instance.startingEntranceIndex).Forget();
		}

		public static async UniTask LoadStartingSceneAsync()
		{
			var entry = Instance.startingSceneEntry;

			if (entry != null) await LoadSceneAsync(entry, Instance.startingEntranceIndex);
		}

		public static async UniTask LoadSceneAsync(SceneEntry sceneEntry, int entranceIndex = -1)
		{
			Dextra.CurrentInputMode = Dextra.InputMode.Unresponsive;
			Chronos.RawTimeScale = 0;

			var volumeTask = Aura.FadeAudioListenerVolumeAsync(0f);

			var faderTask = Propagator.Publish<UniTask>(PropagatorEvents.OnDisplayFaderAsync);

			await UniTask.WhenAll(volumeTask, faderTask);

			await Propagator.Publish<UniTask>(PropagatorEvents.OnDisplayLoadingScreenAsync);

			await Propagator.Publish<UniTask>(PropagatorEvents.OnHideFaderAsync);

			var playerLoadingAction = sceneEntry.playerLoadingAction;

			if (Propagator.Publish<bool>(PropagatorEvents.OnPlayerLoadStateCheck) && playerLoadingAction.Equals(PlayerLoadingAction.Unload))
			{
				Propagator.Publish(PropagatorEvents.OnPlayerUnload);
			}

			var activeScene = Propagator.Publish<SceneEntry>(PropagatorEvents.OnActiveSceneRequested);

			if (activeScene != null)
			{
				await activeScene.OnBeforeUnloadedAsync();
				Propagator.Publish(PropagatorEvents.OnBeforeActiveSceneUnload);

				await Threadlink.UnloadSceneAsync(activeScene.SceneIndexInDatabase);
				Propagator.Publish(PropagatorEvents.OnActiveSceneFinishedUnloading);
			}

			await Threadlink.LoadSceneAsync(sceneEntry.SceneIndexInDatabase, sceneEntry.loadingMode);

			Propagator.Publish(PropagatorEvents.OnNewSceneFinishedLoading, sceneEntry);

			if (Propagator.Publish<bool>(PropagatorEvents.OnPlayerLoadStateCheck) == false && playerLoadingAction.Equals(PlayerLoadingAction.Load))
			{
				await Propagator.Publish<UniTask>(PropagatorEvents.OnPlayerLoadAsync);
			}

			bool playerIsLoaded = Propagator.Publish<bool>(PropagatorEvents.OnPlayerLoadStateCheck);

			if (playerIsLoaded)
			{
				var spawnPoints = sceneEntry.playerSpawnPoints;
				Propagator.Publish(PropagatorEvents.OnPlayerPlaced, entranceIndex.IsWithinBoundsOf(spawnPoints) ? spawnPoints[entranceIndex] : default);
			}

			if (Initium.TryGetInitializableCollection(out var initCollection)) await Initium.BootAndInitCollectionAsync(initCollection);

			await sceneEntry.OnFinishedLoadingAsync();

			Propagator.Publish(PropagatorEvents.OnLoadingProcessFinished);
			Chronos.RawTimeScale = 1;

			await Propagator.Publish<UniTask>(PropagatorEvents.OnDisplayFaderAsync);

			await Propagator.Publish<UniTask>(PropagatorEvents.OnHideLoadingScreenAsync);

			volumeTask = Aura.FadeAudioListenerVolumeAsync(1f);
			faderTask = Propagator.Publish<UniTask>(PropagatorEvents.OnHideFaderAsync);

			//Re-enable player input, if appropriate.
			Dextra.CurrentInputMode = playerIsLoaded ? Dextra.InputMode.Player : Dextra.InputMode.UI;

			await UniTask.WhenAll(volumeTask, faderTask);
		}
	}
}