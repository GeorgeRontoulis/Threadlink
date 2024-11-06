namespace Threadlink.Systems.Nexus
{
	using Aura;
	using Core;
	using Cysharp.Threading.Tasks;
	using Dextra;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.Events;

	/// <summary>
	/// System responsible for scene and player loading during Threadlink's runtime.
	/// </summary>
	public sealed class Nexus : ThreadlinkSystem<Nexus>, IInitializable
	{
		internal enum PlayerLoadingAction { None = -1, Unload, Load }

		private static ThreadlinkEventBus EventBus => Threadlink.EventBus;

		[SerializeField] private SceneEntry startingSceneEntry = null;
		[SerializeField] private int startingEntranceIndex = -1;

		public override Empty Discard(Empty _ = default)
		{
			startingSceneEntry = null;

			return base.Discard(_);
		}

		public void Initialize()
		{
			if (startingSceneEntry != null) LoadSceneAsync(startingSceneEntry, startingEntranceIndex).Forget();
		}

		public static async UniTask LoadSceneAsync(SceneEntry sceneEntry, int entranceIndex = -1)
		{
			Dextra.CurrentInputMode = Dextra.InputMode.Unresponsive;
			Chronos.RawTimeScale = 0;

			var volumeTask = Aura.FadeAudioListenerVolumeAsync(0f);

			var faderTask = EventBus.InvokeOnNexusDisplayFaderAsyncEvent();

			await UniTask.WhenAll(volumeTask, faderTask);

			await EventBus.InvokeOnNexusDisplayLoadingScreenAsyncEvent();

			await EventBus.InvokeOnNexusHideFaderAsyncEvent();

			var playerLoadingAction = sceneEntry.playerLoadingAction;

			if (EventBus.InvokeOnNexusPlayerLoadStateCheckEvent() && playerLoadingAction.Equals(PlayerLoadingAction.Unload))
				EventBus.InvokeOnNexusPlayerUnloadEvent();

			var activeScene = EventBus.InvokeOnNexusActiveSceneRequestedEvent();

			if (activeScene != null)
			{
				await activeScene.OnBeforeUnloadedAsync();
				EventBus.InvokeOnNexusBeforeActiveSceneUnloadEvent();

				await activeScene.AddressableScene.UnloadAsync();
				EventBus.InvokeOnNexusActiveSceneFinishedUnloadingEvent();
			}

			await sceneEntry.AddressableScene.LoadAsync(sceneEntry.loadingMode);

			EventBus.InvokeOnNexusNewSceneFinishedLoadingEvent(sceneEntry);

			if (EventBus.InvokeOnNexusPlayerLoadStateCheckEvent() == false && playerLoadingAction.Equals(PlayerLoadingAction.Load))
			{
				await EventBus.InvokeOnNexusPlayerLoadAsyncEvent();
			}

			bool playerIsLoaded = EventBus.InvokeOnNexusPlayerLoadStateCheckEvent();

			if (playerIsLoaded)
			{
				var spawnPoints = sceneEntry.playerSpawnPoints;
				EventBus.InvokeOnNexusPlayerPlacedEvent(entranceIndex.IsWithinBoundsOf(spawnPoints) ?
				spawnPoints[entranceIndex] : default);
			}

			if (Initium.TryGetInitializableCollection(out var initCollection)) await Initium.BootAndInitCollectionAsync(initCollection);

			await sceneEntry.OnFinishedLoadingAsync();

			EventBus.InvokeOnNexusLoadingProcessFinishedEvent();
			Chronos.RawTimeScale = 1;

			await EventBus.InvokeOnNexusDisplayFaderAsyncEvent();

			await EventBus.InvokeOnNexusHideLoadingScreenAsyncEvent();

			volumeTask = Aura.FadeAudioListenerVolumeAsync(1f);
			faderTask = EventBus.InvokeOnNexusHideFaderAsyncEvent();

			//Re-enable player input, if appropriate.
			Dextra.CurrentInputMode = playerIsLoaded ? Dextra.InputMode.Player : Dextra.InputMode.UI;

			await UniTask.WhenAll(volumeTask, faderTask);
		}
	}
}