namespace Threadlink.Core
{
	using Extensions.Addressables;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Systems;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;
	using Utilities.UnityLogging;
	using String = Utilities.Text.String;


	#region Threadlink Delegate Definitions

	public delegate void GenericVoidDelegate<T, T1>(T arg0, T1 arg1);
	public delegate void GenericVoidDelegate<T>(T argument);
	public delegate T GenericDelegate<T>();
	public delegate void VoidDelegate();

	#endregion

	/// <summary>
	/// A class representing a Threadlink-Compatible Coroutine.
	/// This should only be used in combination with the ThreadlinkCoroutineBatch.
	/// </summary>
	internal sealed class ThreadlinkCoroutine
	{
		private IEnumerator InternalProcess { get; set; }

		internal ThreadlinkCoroutine(IEnumerator targetCoroutine)
		{
			InternalProcess = targetCoroutine;
		}

		internal IEnumerator Monitor(Action onCompleted)
		{
			yield return InternalProcess;

			onCompleted?.Invoke();

			InternalProcess = null;
		}
	}

	/// <summary>
	/// A container of Threadlink-Compatible coroutines.
	/// The container automatically runs all of its coroutines immeditally
	/// after initialization. You can check when all coroutines have finished
	/// executing by checking the IsDone property.
	/// </summary>
	public sealed class ThreadlinkCoroutineBatch
	{
		public bool IsDone => CoroutineCount <= 0;

		private int CoroutineCount { get; set; }

		public ThreadlinkCoroutineBatch(params IEnumerator[] coroutines)
		{
			CoroutineCount = coroutines.Length;

			ThreadlinkCoroutine[] trackableCoroutines = new ThreadlinkCoroutine[CoroutineCount];

			for (int i = 0; i < CoroutineCount; i++)
			{
				trackableCoroutines[i] = new ThreadlinkCoroutine(coroutines[i]);
				Threadlink.LaunchCoroutine(trackableCoroutines[i].Monitor(DecreaseCountByOne), false);
			}
		}

		private void DecreaseCountByOne() { CoroutineCount--; }
	}

	/// <summary>
	/// The Core Game System. Controls all aspects of the runtime
	/// and manages sub-systems and entities at the lowest level.
	/// </summary>
	public sealed class Threadlink : MonoBehaviour
	{
		private const string ID = "Threadlink";

		public static Threadlink Instance { get; private set; }

		private static ThreadlinkAddressables Addressables => Instance.addressables;

		private List<BaseLinkableSystem> LinkedSystems { get; set; }

		private static WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

		[SerializeField] private ThreadlinkAddressables addressables = null;

		#region Initialization and Deployment
		private void Awake() { Instance = this; }
		private IEnumerator Start() { yield return Deploy(); }

		private IEnumerator Deploy()
		{
			addressables = Instantiate(addressables);
			addressables.customExtender = Instantiate(addressables.customExtender);

			LinkedSystems = new List<BaseLinkableSystem>();

			ThreadlinkCoroutineBatch coroutineBatch = new ThreadlinkCoroutineBatch(addressables.LoadCoreSystems());

			while (coroutineBatch.IsDone == false) yield return null;

			ThreadlinkAddressables.SystemAddressable[] systems = addressables.coreSystems;
			int length = systems.Length;

			for (int i = 0; i < length; i++) Link(systems[i].Result);

			yield return Initium.BootLinkableObjects(LinkedSystems);
			yield return Initium.InitializeLinkableObjects(LinkedSystems);

			Scribe.SystemLog(ID, DebugNotificationType.Info, "Threadlink Core successfully deployed. All Systems operational.");
		}

		public static void ShutDown()
		{
			List<BaseLinkableSystem> systems = Instance.LinkedSystems;

			for (int i = systems.Count - 1; i >= 0; i--) Sever(systems[i]);

#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
		}
		#endregion

		#region Core Logic
		internal static BaseLinkableSystem Link(BaseLinkableSystem original)
		{
			if (original == null)
			{
				Scribe.SystemLog(ID, DebugNotificationType.Error, "The requested System to link is NULL!");
				return null;
			}

			BaseLinkableSystem copy = Instantiate(original, Instance.transform);

			Instance.LinkedSystems.Add(copy);

			copy.name = original.name;

			Scribe.SystemLog(ID, DebugNotificationType.Info, "Linked ", copy.name, ".");

			return copy;
		}

		internal static void Sever(BaseLinkableSystem system)
		{
			if (system == null)
			{
				Scribe.SystemLog(ID, DebugNotificationType.Error, "The requested System to sever is NULL!");
				return;
			}

			void DiscardTargetSystem() { system.Discard(); }

			List<BaseLinkableSystem> collection = Instance.LinkedSystems;

			if (collection.Contains(system) == false)
			{
				collection.RemoveEfficiently(collection.IndexOf(system));
				DiscardTargetSystem();
			}
			else
			{
				Scribe.SystemLog(ID, DebugNotificationType.Error, "A Sever request was made for System ", system.name,
				", however it's not managed by ", ID, " . This is probably a memory leak and should never happen! Discarding the Entity...");

				DiscardTargetSystem();
			}
		}
		#endregion

		#region Coroutine Management
		public static Coroutine LaunchCoroutine(IEnumerator coroutine, bool logLaunch = true)
		{
			if (logLaunch)
			{
				Scribe.SystemLog(ID, DebugNotificationType.Info,
				"Launching Coroutine '", String.ExtractCoroutineName(coroutine), "'");
			}

			return Instance.StartCoroutine(coroutine);
		}

		/// <summary>
		/// Stops the desired coroutine and nullifies the reference to it.
		/// </summary>
		/// <param name="coroutine">The coroutine to stop.</param>
		public static void StopCoroutine(ref Coroutine coroutine)
		{
			if (coroutine == null) return;

			Instance.StopCoroutine(coroutine);
			coroutine = null;

			Scribe.SystemLog(ID, DebugNotificationType.Info, "Stopped Coroutine ", coroutine.GetType().Name);
		}

		public static IEnumerator WaitForFrameCount(int count)
		{
			if (count <= 0)
			{
				Scribe.SystemLog(ID, DebugNotificationType.Warning, "Attempted to wait an invalid number of frames! Call will be skipped!");
				yield break;
			}

			int framesWaited = 0;

			while (framesWaited < count)
			{
				yield return waitForEndOfFrame;

				framesWaited++;
			}
		}
		#endregion

		#region Addressables Lookup Methods
		public static AddressableType SearchExtenderForAddressablePrefab<AddressableType, PrefabType>(string prefabID)
		where AddressableType : AddressablePrefab<PrefabType> where PrefabType : Component
		{
			ThreadlinkAddressablesExtender extender = Addressables.customExtender;

			if (extender != null) return extender.SearchForAddressablePrefab<AddressableType, PrefabType>(prefabID);
			else
			{
				Scribe.SystemLog(ID, DebugNotificationType.Error,
				"A request to search the Addressables Extender was made, however no extender has been provided! Please provide an extender before proceeding!");
				return null;
			}
		}

		public static AddressableType SearchExtenderForAddressableAsset<AddressableType, AssetType>(string assetID)
		where AddressableType : AddressableAsset<AssetType> where AssetType : UnityEngine.Object
		{
			ThreadlinkAddressablesExtender extender = Addressables.customExtender;

			if (extender != null) return extender.SearchForAddressableAsset<AddressableType, AssetType>(assetID);
			else
			{
				Scribe.SystemLog(ID, DebugNotificationType.Error,
				"A request to search the Addressables Extender was made, however no extender has been provided! Please provide an extender before proceeding!");
				return null;
			}
		}

		public static T GetCustomExtender<T>() where T : ThreadlinkAddressablesExtender
		{
			return Addressables.customExtender as T;
		}

		public static AddressableScene FindSceneAddressable(string address)
		{
			return Addressables.scenes.BinarySearch(address);
		}
		#endregion
	}
}