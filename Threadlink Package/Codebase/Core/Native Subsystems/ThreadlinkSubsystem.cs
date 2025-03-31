namespace Threadlink.Core
{
	using Core.Exceptions;
	using Cysharp.Threading.Tasks;
	using Subsystems.Scribe;
	using System.Collections.Generic;

#if UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public interface IUnityWeaver<S, E> : IThreadlinkSingleton<S> { public T Weave<T>(T original) where T : E; }
	public interface IAddressablesPreloader { public UniTask PreloadAssetsAsync(); }

	public abstract class ThreadlinkSubsystem : LinkableBehaviour, IBootable
	{
		public abstract void Boot();
	}

	public abstract class ThreadlinkSubsystem<S> : ThreadlinkSubsystem, IThreadlinkSingleton<S>
	where S : ThreadlinkSubsystem<S>
	{
		public static S Instance { get; protected set; }

		public override void Boot()
		{
			var thisSystem = this as S;

			if (Instance == null || Instance.Equals(thisSystem) == false) Instance = thisSystem;
			else throw new ExistingSingletonException(Scribe.FromSubsystem<S>("This Subsystem already exists!").ToString());
		}

		public override void Discard()
		{
			Instance = null;
			base.Discard();
		}
	}

	public abstract class Register<S, E> : ThreadlinkSubsystem<S>
	where S : Register<S, E>
	where E : IIdentifiable
	{
#if UNITY_EDITOR && ODIN_INSPECTOR
		[ShowInInspector, ReadOnly]
#endif
		protected Dictionary<int, E> Registry { get; private set; }

		protected void ClearRegistry(bool trimRegistry = false)
		{
			Registry.Clear();
			if (trimRegistry) Registry.TrimExcess();
		}

		public override void Discard()
		{
			ClearRegistry(true);
			Registry = null;
			base.Discard();
		}

		public override void Boot()
		{
			Registry = new();
			base.Boot();
		}

		public bool HasLinkedEntity(int linkID) => Registry.ContainsKey(linkID);
		public bool TryGetLinkedEntity(int linkID, out E entity) => Registry.TryGetValue(linkID, out entity);

		public bool TryGetLinkedEntity(string singletonID, out E entity)
		{
			if (Threadlink.TryGetConstantSingletonID(singletonID, out var id) == false)
			{
				entity = default;
				return false;
			}

			return Registry.TryGetValue(id, out entity) && entity != null;
		}
	}

	public abstract class Linker<S, E> : Register<S, E>
	where S : Linker<S, E>
	where E : IIdentifiable
	{
		public virtual bool TryLink(E entity) => Registry.TryAdd(entity.ID, entity);
		public virtual bool TryDisconnect(int linkID, out E disconnectedEntity) => Registry.Remove(linkID, out disconnectedEntity);
		public virtual void DisconnectAll(bool trimRegistry) => ClearRegistry(trimRegistry);
	}

	public abstract class Weaver<S, E> : Register<S, E>
	where S : Weaver<S, E>
	where E : IDiscardable, IIdentifiable
	{
		public virtual bool TrySever(int linkID)
		{
			bool severed = Registry.Remove(linkID, out var handle);

			if (severed) handle?.Discard();

			return severed;
		}

		public virtual void SeverAll()
		{
			foreach (var id in Registry.Keys)
			{
				var entity = Registry[id];

#pragma warning disable IDE0031 // Use null propagation
				if (entity != null) entity.Discard(); //Unity weavers cannot use null propagation.
#pragma warning restore IDE0031 // Use null propagation
			}

			ClearRegistry();
		}
	}

	public abstract class UnityWeaver<S, E> : Weaver<S, E>, IUnityWeaver<S, E>
	where S : UnityWeaver<S, E>
	where E : UnityEngine.Object, IDiscardable, IIdentifiable
	{
		public virtual T Weave<T>(T original) where T : E
		{
			static T CreateNewInstance(ref T original)
			{
				var newInstance = Instantiate(original);
				newInstance.name = original.name;
				if (newInstance is LinkableAsset asset) asset.IsInstance = true;

				return newInstance;
			}

			if (original is IThreadlinkSingleton)
			{
				if (Threadlink.TryGetConstantSingletonID(original.name, out var id))
				{
					if (TryGetLinkedEntity(id, out var singleton) && singleton != null) return singleton as T;
					else throw new CorruptConstantsBufferException(Scribe.FromSubsystem<S>("Constant IDs Buffer mismatch detected!").ToString());
				}
				else
				{
					var singleton = CreateNewInstance(ref original);

					Threadlink.RegisterConstantSingletonID(singleton.name, singleton.ID);
					Registry.Add(singleton.ID, singleton);

					if (singleton is not ThreadlinkSubsystem) DontDestroyOnLoad(singleton);
					return singleton;
				}
			}
			else
			{
				var entity = CreateNewInstance(ref original);
				Registry.Add(entity.ID, entity);

				return entity;
			}
		}
	}
}
