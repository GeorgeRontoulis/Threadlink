namespace Threadlink.Systems.Nexus
{
	using System;
	using System.Collections;
	using System.Linq;
	using Threadlink.Core;
	using Threadlink.Extensions.Nexus;
	using Threadlink.Systems.Initium;
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// System responsible for scene and player loading during Threadlink's runtime.
	/// </summary>
	public sealed class Nexus : LinkableSystem<LinkableEntity>
	{
		public static Nexus Instance { get; private set; }

		public static event VoidDelegate OnBeforeSceneUnload
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onBeforeSceneUnload;

				if (myEvent.Contains(value) == false) myEvent += value;
			}

			remove { Instance.onBeforeSceneUnload -= value; }
		}

		public static event VoidDelegate OnAfterSceneUnload
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onAfterSceneUnload;

				if (myEvent.Contains(value) == false) myEvent += value;
			}

			remove { Instance.onAfterSceneUnload -= value; }
		}

		public static event GenericVoidDelegate<SceneEntry> OnAfterSceneLoad
		{
			add
			{
				ref GenericVoidDelegate<SceneEntry> myEvent = ref Instance.onAfterSceneLoad;

				if (myEvent.Contains(value) == false) myEvent += value;
			}

			remove { Instance.onAfterSceneLoad -= value; }
		}

		public static event GenericDelegate<SceneEntry> OnActiveSceneRetrieval
		{
			add
			{
				ref GenericDelegate<SceneEntry> myEvent = ref Instance.onActiveSceneRetrieval;

				if (myEvent == null || myEvent.GetListenerCount() == 0) myEvent += value;
			}

			remove { Instance.onActiveSceneRetrieval -= value; }
		}

		public static event GenericDelegate<IEnumerator> OnDisplayFader
		{
			add
			{
				ref GenericDelegate<IEnumerator> myEvent = ref Instance.onDisplayFader;

				if (myEvent == null || myEvent.GetListenerCount() == 0) myEvent += value;
			}

			remove { Instance.onDisplayFader -= value; }
		}

		public static event GenericDelegate<IEnumerator> OnHideFader
		{
			add
			{
				ref GenericDelegate<IEnumerator> myEvent = ref Instance.onHideFader;

				if (myEvent == null || myEvent.GetListenerCount() == 0) myEvent += value;
			}

			remove { Instance.onHideFader -= value; }
		}

		public static event GenericDelegate<IEnumerator> OnDisplayLoadingScreen
		{
			add
			{
				ref GenericDelegate<IEnumerator> myEvent = ref Instance.onDisplayLoadingScreen;

				if (myEvent == null || myEvent.GetListenerCount() == 0) myEvent += value;
			}

			remove { Instance.onDisplayLoadingScreen -= value; }
		}

		public static event GenericDelegate<IEnumerator> OnHideLoadingScreen
		{
			add
			{
				ref GenericDelegate<IEnumerator> myEvent = ref Instance.onHideLoadingScreen;

				if (myEvent == null || myEvent.GetListenerCount() == 0) myEvent += value;
			}

			remove { Instance.onHideLoadingScreen -= value; }
		}

		public static BasePlayerLoaderExtension CustomPlayerLoader => Instance.customPlayerLoader;

		[SerializeField] private SceneEntry startingSceneEntry = null;

		[Space(10)]

		[SerializeField] private BasePlayerLoaderExtension customPlayerLoader = null;

		private event VoidDelegate onBeforeSceneUnload = null;
		private event VoidDelegate onAfterSceneUnload = null;
		private event GenericVoidDelegate<SceneEntry> onAfterSceneLoad = null;
		private event GenericDelegate<SceneEntry> onActiveSceneRetrieval = null;
		private event GenericDelegate<IEnumerator> onDisplayFader = null;
		private event GenericDelegate<IEnumerator> onDisplayLoadingScreen = null;
		private event GenericDelegate<IEnumerator> onHideFader = null;
		private event GenericDelegate<IEnumerator> onHideLoadingScreen = null;

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
			Threadlink.LaunchCoroutine(SceneLoadingCoroutine(startingSceneEntry), false);
		}

		public static IEnumerator SceneLoadingCoroutine(SceneEntry sceneEntry)
		{
			//Pause the game.
			Chronos.RawTimeScale = 0;
			//Display the Fader and wait until it is fully visible.
			if (Instance.onDisplayFader != null) yield return Instance.onDisplayFader.Invoke();
			//Display the Loading Screen and wait until it is fully visible.
			if (Instance.onDisplayLoadingScreen != null) yield return Instance.onDisplayLoadingScreen.Invoke();
			//Hide the Fader and wait until it is fully hidden.
			if (Instance.onHideFader != null) yield return Instance.onHideFader.Invoke();

			PlayerLoadingAction playerLoadingAction = sceneEntry.playerLoadingAction;
			BasePlayerLoaderExtension playerLoader = Instance.customPlayerLoader;

			if (playerLoader != null && playerLoader.PlayerIsLoaded && playerLoadingAction.Equals(PlayerLoadingAction.Unload)) playerLoader.Unload();

			SceneEntry RetrieveActiveScene() { return Instance.onActiveSceneRetrieval(); }
			SceneEntry activeScene = RetrieveActiveScene();

			if (activeScene != null)
			{
				Instance.onBeforeSceneUnload?.Invoke();

				yield return activeScene.AddressableScene.UnloadingCoroutine();

				Instance.onAfterSceneUnload?.Invoke();
			}

			yield return sceneEntry.AddressableScene.LoadingCoroutine(sceneEntry.loadingMode);

			Instance.onAfterSceneLoad?.Invoke(sceneEntry);
			activeScene = RetrieveActiveScene();

			if (playerLoader != null && playerLoader.PlayerIsLoaded == false && playerLoadingAction.Equals(PlayerLoadingAction.Load))
			{
				yield return playerLoader.LoadingCoroutine();
			}

			InitializableCollection initCollection = Initium.GetSceneInitCollection(activeScene.AddressableScene.Result.Scene.name);
			if (initCollection != null) yield return Initium.BootAndInitCollection(initCollection);

			yield return sceneEntry.PostLoadingCoroutine();

			if (Instance.onDisplayFader != null) yield return Instance.onDisplayFader.Invoke();

			if (Instance.onHideLoadingScreen != null) yield return Instance.onHideLoadingScreen.Invoke();

			if (Instance.onHideFader != null) yield return Instance.onHideFader.Invoke();

			Chronos.RawTimeScale = 1;
		}
	}
}