namespace Threadlink.Core
{
	using Cysharp.Threading.Tasks;
	using Systems.Dextra;
	using Systems.Nexus;
	using UnityEngine;
	using Utilities.Events;

	public partial class ThreadlinkEventBus
	{
		public event ThreadlinkDelegate<Empty, Empty> OnIrisUpdate
		{
			add => onIrisUpdate.OnInvoke += value;
			remove => onIrisUpdate.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnIrisFixedUpdate
		{
			add => onIrisFixedUpdate.OnInvoke += value;
			remove => onIrisFixedUpdate.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnIrisLateUpdate
		{
			add => onIrisLateUpdate.OnInvoke += value;
			remove => onIrisLateUpdate.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnChronosPaused
		{
			add => onChronosPaused.OnInvoke += value;
			remove => onChronosPaused.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnChronosResumed
		{
			add => onChronosResumed.OnInvoke += value;
			remove => onChronosResumed.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, float> OnChronosPlaytimeCount
		{
			add => onChronosPlaytimeCount.OnInvoke += value;
			remove => onChronosPlaytimeCount.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, RectTransform> OnDextraSelected
		{
			add => onDextraSelected.OnInvoke += value;
			remove => onDextraSelected.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Dextra.InputDevice> OnDextraDeviceChanged
		{
			add => onDextraDeviceChanged.OnInvoke += value;
			remove => onDextraDeviceChanged.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Dextra.InputMode> OnDextraInputModeChanged
		{
			add => onDextraInputModeChanged.OnInvoke += value;
			remove => onDextraInputModeChanged.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnDextraInteractPressed
		{
			add => onDextraInteractPressed.OnInvoke += value;
			remove => onDextraInteractPressed.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnDextraPausePressed
		{
			add => onDextraPausePressed.OnInvoke += value;
			remove => onDextraPausePressed.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnDextraInterfaceCancelled
		{
			add => onDextraInterfaceCancelled.OnInvoke += value;
			remove => onDextraInterfaceCancelled.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnNexusBeforeActiveSceneUnload
		{
			add => onNexusBeforeActiveSceneUnload.OnInvoke += value;
			remove => onNexusBeforeActiveSceneUnload.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnNexusActiveSceneFinishedUnloading
		{
			add => onNexusActiveSceneFinishedUnloading.OnInvoke += value;
			remove => onNexusActiveSceneFinishedUnloading.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnNexusLoadingProcessFinished
		{
			add => onNexusLoadingProcessFinished.OnInvoke += value;
			remove => onNexusLoadingProcessFinished.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Empty> OnNexusPlayerUnload
		{
			add => onNexusPlayerUnload.OnInvoke += value;
			remove => onNexusPlayerUnload.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, SceneEntry> OnNexusNewSceneFinishedLoading
		{
			add => onNexusNewSceneFinishedLoading.OnInvoke += value;
			remove => onNexusNewSceneFinishedLoading.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<SceneEntry, Empty> OnNexusActiveSceneRequested
		{
			add => onNexusActiveSceneRequested.OnInvoke += value;
			remove => onNexusActiveSceneRequested.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<bool, Empty> OnNexusPlayerLoadStateCheck
		{
			add => onNexusPlayerLoadStateCheck.OnInvoke += value;
			remove => onNexusPlayerLoadStateCheck.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<Empty, Vector3> OnNexusPlayerPlaced
		{
			add => onNexusPlayerPlaced.OnInvoke += value;
			remove => onNexusPlayerPlaced.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<UniTask, Empty> OnNexusPlayerLoadAsync
		{
			add => onNexusPlayerLoadAsync.OnInvoke += value;
			remove => onNexusPlayerLoadAsync.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<UniTask, Empty> OnNexusDisplayFaderAsync
		{
			add => onNexusDisplayFaderAsync.OnInvoke += value;
			remove => onNexusDisplayFaderAsync.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<UniTask, Empty> OnNexusDisplayLoadingScreenAsync
		{
			add => onNexusDisplayLoadingScreenAsync.OnInvoke += value;
			remove => onNexusDisplayLoadingScreenAsync.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<UniTask, Empty> OnNexusHideFaderAsync
		{
			add => onNexusHideFaderAsync.OnInvoke += value;
			remove => onNexusHideFaderAsync.OnInvoke -= value;
		}

		public event ThreadlinkDelegate<UniTask, Empty> OnNexusHideLoadingScreenAsync
		{
			add => onNexusHideLoadingScreenAsync.OnInvoke += value;
			remove => onNexusHideLoadingScreenAsync.OnInvoke -= value;
		}


		private readonly VoidEvent onIrisUpdate = new();
		private readonly VoidEvent onIrisFixedUpdate = new();
		private readonly VoidEvent onIrisLateUpdate = new();
		private readonly VoidEvent onChronosPaused = new();
		private readonly VoidEvent onChronosResumed = new();
		private readonly GenericInputEvent<float> onChronosPlaytimeCount = new();
		private readonly GenericInputEvent<RectTransform> onDextraSelected = new();
		private readonly GenericInputEvent<Dextra.InputDevice> onDextraDeviceChanged = new();
		private readonly GenericInputEvent<Dextra.InputMode> onDextraInputModeChanged = new();
		public readonly VoidEvent onDextraInteractPressed = new();
		private readonly VoidEvent onDextraPausePressed = new();
		private readonly VoidEvent onDextraInterfaceCancelled = new();
		private readonly VoidEvent onNexusBeforeActiveSceneUnload = new();
		private readonly VoidEvent onNexusActiveSceneFinishedUnloading = new();
		private readonly VoidEvent onNexusLoadingProcessFinished = new();
		private readonly VoidEvent onNexusPlayerUnload = new();
		private readonly GenericInputEvent<SceneEntry> onNexusNewSceneFinishedLoading = new();
		private readonly GenericOutputEvent<SceneEntry> onNexusActiveSceneRequested = new();
		private readonly GenericOutputEvent<bool> onNexusPlayerLoadStateCheck = new();
		private readonly GenericInputEvent<Vector3> onNexusPlayerPlaced = new();
		private readonly GenericOutputEvent<UniTask> onNexusPlayerLoadAsync = new();
		private readonly GenericOutputEvent<UniTask> onNexusDisplayFaderAsync = new();
		private readonly GenericOutputEvent<UniTask> onNexusDisplayLoadingScreenAsync = new();
		private readonly GenericOutputEvent<UniTask> onNexusHideFaderAsync = new();
		private readonly GenericOutputEvent<UniTask> onNexusHideLoadingScreenAsync = new();

		public Empty InvokeOnIrisUpdateEvent(Empty input = default)
		{
			return onIrisUpdate.Invoke(input);
		}
		public Empty InvokeOnIrisFixedUpdateEvent(Empty input = default)
		{
			return onIrisFixedUpdate.Invoke(input);
		}
		public Empty InvokeOnIrisLateUpdateEvent(Empty input = default)
		{
			return onIrisLateUpdate.Invoke(input);
		}
		public Empty InvokeOnChronosPausedEvent(Empty input = default)
		{
			return onChronosPaused.Invoke(input);
		}
		public Empty InvokeOnChronosResumedEvent(Empty input = default)
		{
			return onChronosResumed.Invoke(input);
		}
		public Empty InvokeOnChronosPlaytimeCountEvent(float input = default)
		{
			return onChronosPlaytimeCount.Invoke(input);
		}
		public Empty InvokeOnDextraSelectedEvent(RectTransform input = default)
		{
			return onDextraSelected.Invoke(input);
		}
		public Empty InvokeOnDextraDeviceChangedEvent(Dextra.InputDevice input = default)
		{
			return onDextraDeviceChanged.Invoke(input);
		}
		public Empty InvokeOnDextraInputModeChangedEvent(Dextra.InputMode input = default)
		{
			return onDextraInputModeChanged.Invoke(input);
		}
		public Empty InvokeOnDextraInteractPressedEvent(Empty input = default)
		{
			return onDextraInteractPressed.Invoke(input);
		}
		public Empty InvokeOnDextraPausePressedEvent(Empty input = default)
		{
			return onDextraPausePressed.Invoke(input);
		}
		public Empty InvokeOnDextraInterfaceCancelledEvent(Empty input = default)
		{
			return onDextraInterfaceCancelled.Invoke(input);
		}
		public Empty InvokeOnNexusBeforeActiveSceneUnloadEvent(Empty input = default)
		{
			return onNexusBeforeActiveSceneUnload.Invoke(input);
		}
		public Empty InvokeOnNexusActiveSceneFinishedUnloadingEvent(Empty input = default)
		{
			return onNexusActiveSceneFinishedUnloading.Invoke(input);
		}
		public Empty InvokeOnNexusLoadingProcessFinishedEvent(Empty input = default)
		{
			return onNexusLoadingProcessFinished.Invoke(input);
		}
		public Empty InvokeOnNexusPlayerUnloadEvent(Empty input = default)
		{
			return onNexusPlayerUnload.Invoke(input);
		}
		public Empty InvokeOnNexusNewSceneFinishedLoadingEvent(SceneEntry input = default)
		{
			return onNexusNewSceneFinishedLoading.Invoke(input);
		}
		public SceneEntry InvokeOnNexusActiveSceneRequestedEvent(Empty input = default)
		{
			return onNexusActiveSceneRequested.Invoke(input);
		}
		public bool InvokeOnNexusPlayerLoadStateCheckEvent(Empty input = default)
		{
			return onNexusPlayerLoadStateCheck.Invoke(input);
		}
		public Empty InvokeOnNexusPlayerPlacedEvent(Vector3 input = default)
		{
			return onNexusPlayerPlaced.Invoke(input);
		}
		public UniTask InvokeOnNexusPlayerLoadAsyncEvent(Empty input = default)
		{
			return onNexusPlayerLoadAsync.Invoke(input);
		}
		public UniTask InvokeOnNexusDisplayFaderAsyncEvent(Empty input = default)
		{
			return onNexusDisplayFaderAsync.Invoke(input);
		}
		public UniTask InvokeOnNexusDisplayLoadingScreenAsyncEvent(Empty input = default)
		{
			return onNexusDisplayLoadingScreenAsync.Invoke(input);
		}
		public UniTask InvokeOnNexusHideFaderAsyncEvent(Empty input = default)
		{
			return onNexusHideFaderAsync.Invoke(input);
		}
		public UniTask InvokeOnNexusHideLoadingScreenAsyncEvent(Empty input = default)
		{
			return onNexusHideLoadingScreenAsync.Invoke(input);
		}
	}
}