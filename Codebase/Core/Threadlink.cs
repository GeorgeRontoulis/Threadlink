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

			yield return Initium.Boot(LinkedEntities);
			yield return Initium.Initialize(LinkedEntities);

			Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Threadlink successfully deployed. All Systems operational.");
		}

		public static void ShutDown()
		{
			Instance.Discard();
		}

		public override void Discard()
		{
			SeverAll();

			addressables = null;
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
				Scribe.SystemLog(Instance.LinkID, Scribe.InfoNotif, "Launching Coroutine '", coroutine.ExtractName(), "'");

			return Instance.StartCoroutine(coroutine);
		}

		/// <summary>
		/// Stops the desired coroutine and nullifies the reference to it.
		/// </summary>
		/// <param name="coroutine">The coroutine to stop.</param>
		public static void StopCoroutine(ref Coroutine coroutine, bool logStop = false)
		{
			if (coroutine == null) return;

			Instance.StopCoroutine(coroutine);

			if (logStop) Scribe.SystemLog(Instance.LinkID,
			Scribe.InfoNotif, "Stopped Coroutine ", coroutine.GetType().Name);

			coroutine = null;
		}

		public static IEnumerator WaitForFrameCount(int count)
		{
			if (count <= 0)
			{
				Scribe.SystemLog(Instance.LinkID, Scribe.WarningNotif, "Attempted to wait a non-positive number of frames! Call will be skipped!");
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
		public static void FindAddressablePrefab<AddressableType, PrefabType>(string prefabID, out AddressableType result)
		where AddressableType : AddressablePrefab<PrefabType> where PrefabType : Component
		{
			var extension = Addressables.customExtension;

			if (extension != null)
			{
				extension.SearchForAddressablePrefab<AddressableType, PrefabType>(prefabID, out var prefab);
				result = prefab;
				return;
			}
			else
			{
				Scribe.SystemLog(Instance.LinkID, Scribe.ErrorNotif,
				"A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!");
				result = null;
			}
		}

		public static void FindAddressableAsset<AddressableType, AssetType>(string assetID, out AddressableType result)
		where AddressableType : AddressableAsset<AssetType> where AssetType : UnityEngine.Object
		{
			var extension = Addressables.customExtension;

			if (extension != null)
			{
				extension.SearchForAddressableAsset<AddressableType, AssetType>(assetID, out var asset);
				result = asset;
				return;
			}
			else
			{
				Scribe.SystemLog(Instance.LinkID, Scribe.ErrorNotif,
				"A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!");
				result = null;
			}
		}

		public static void FindAddressableScene(string address, out AddressableScene result)
		{
			Addressables.scenes.BinarySearch(address, out var addressable);
			result = addressable;
		}

		public static void GetCustomAddressablesExtension<T>(out T result) where T : ThreadlinkAddressablesExtension
		{
			var extension = Addressables.customExtension;

			if (extension == null)
			{
				result = null;
				return;
			}

			result = extension as T;
		}
		#endregion
	}
}