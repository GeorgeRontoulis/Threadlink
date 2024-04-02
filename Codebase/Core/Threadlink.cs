namespace Threadlink.Core
{
	using Extensions.Addressables;
	using System;
	using System.Collections;
	using Systems;
	using Systems.Initium;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;
	using Utilities.Editor.Attributes;
	using Utilities.UnityLogging;
	using Utilities.Text;

	public static class ThreadlinkCoreExtensionMethods
	{
		public static T Clone<T>(this T original) where T : LinkableAsset
		{
			var copy = UnityEngine.Object.Instantiate(original);

			copy.name = original.name;
			copy.IsInstance = true;

			return copy;
		}
	}

	/// <summary>
	/// The Core Game System. Controls all aspects of the runtime
	/// and manages sub-systems and entities at the lowest level.
	/// </summary>
	public sealed class Threadlink : ThreadlinkSystem<Threadlink, LinkableBehaviour>
	{
		private static readonly WaitForEndOfFrame waitForEndOfFrame = new();

		private static ThreadlinkAddressables Addressables => Instance.addressables;

		public override string LinkID => "Threadlink";

		[ReadOnly][SerializeField] private ThreadlinkAddressables addressables = null;

		#region Initialization and Deployment
		private void Awake()
		{
			Instance = this;
			Boot();
		}

		private IEnumerator Start()
		{
			Initialize();
			yield return Deploy();
		}

		public override void Initialize() { }

		private IEnumerator Deploy()
		{
			var coroutineBatch = new ThreadlinkCoroutineBatch(addressables.LoadCoreSystems());

			while (coroutineBatch.IsDone == false) yield return null;

			var systems = addressables.coreSystems;
			int length = systems.Length;

			for (int i = 0; i < length; i++) Weave(systems[i].Result);

			yield return Initium.Boot(Instance.LinkedEntities);
			yield return Initium.Initialize(Instance.LinkedEntities);

			Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Threadlink successfully deployed. All Systems operational.");
		}

		public static void ShutDown()
		{
			Instance.Discard();
		}

		public override void Discard()
		{
			SeverAll();

			Instance.addressables = null;
			Instance = null;

#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
		}
		#endregion

		#region Coroutine Management
		public static Coroutine LaunchCoroutine(IEnumerator coroutine, bool logLaunch = false)
		{
			if (logLaunch)
			{
				Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Info,
				"Launching Coroutine '", coroutine.ExtractName(), "'");
			}

			return Instance.StartCoroutine(coroutine);
		}

		/// <summary>
		/// Stops the desired coroutine and nullifies the reference to it.
		/// </summary>
		/// <param name="coroutine">The coroutine to stop.</param>
		public static void StopCoroutine(ref Coroutine coroutine, bool logStop = false)
		{
			if (coroutine == null) return;

			string coroutineName = coroutine.GetType().Name;

			Instance.StopCoroutine(coroutine);
			coroutine = null;

			if (logStop) Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Info, "Stopped Coroutine ", coroutineName);
		}

		public static IEnumerator WaitForFrameCount(int count)
		{
			if (count <= 0)
			{
				Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Warning, "Attempted to wait a non-positive number of frames! Call will be skipped!");
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
		public static AddressableType FindAddressablePrefab<AddressableType, PrefabType>(string prefabID)
		where AddressableType : AddressablePrefab<PrefabType> where PrefabType : Component
		{
			var extension = Addressables.customExtension;

			if (extension != null) return extension.SearchForAddressablePrefab<AddressableType, PrefabType>(prefabID);
			else
			{
				Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Error,
				"A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!");
				return null;
			}
		}

		public static AddressableType FindAddressableAsset<AddressableType, AssetType>(string assetID)
		where AddressableType : AddressableAsset<AssetType> where AssetType : UnityEngine.Object
		{
			var extension = Addressables.customExtension;

			if (extension != null) return extension.SearchForAddressableAsset<AddressableType, AssetType>(assetID);
			else
			{
				Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Error,
				"A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!");
				return null;
			}
		}

		public static AddressableScene FindAddressableScene(string address)
		{
			return Addressables.scenes.BinarySearch(address);
		}

		public static T GetCustomAddressablesExtension<T>() where T : ThreadlinkAddressablesExtension
		{
			var extension = Addressables.customExtension;

			if (extension == null) return null;

			return extension as T;
		}
		#endregion
	}
}