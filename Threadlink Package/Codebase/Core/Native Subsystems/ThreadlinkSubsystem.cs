namespace Threadlink.Core
{
	using Core.Exceptions;
	using Cysharp.Threading.Tasks;
	using ExtensionMethods;
	using Subsystems.Scribe;
	using System;
	using System.Collections.Generic;

#if UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public interface IUnityWeaver<S, E> : IThreadlinkSingleton<S> { public E Weave(E original); }
	public interface INativeWeaver<S, E> : IThreadlinkSingleton<S> { public E Weave(); }
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
	where E : ILinkable<Ulid>
	{
#if UNITY_EDITOR && ODIN_INSPECTOR
		[ShowInInspector, ReadOnly]
#endif
		protected Dictionary<Ulid, E> Registry { get; private set; }

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

		public bool TryGetLinkedEntity(Ulid linkID, out E entity)
		{
			return Registry.TryGetValue(linkID, out entity);
		}

		public bool TryGetLinkedEntity(string singletonID, out E entity)
		{
			if (Threadlink.TryGetConstantSingletonID(singletonID, out var id) == false)
			{
				entity = default;
				return false;
			}

			return Registry.TryGetValue(id, out entity);
		}
	}

	public abstract class Linker<S, E> : Register<S, E>
	where S : Linker<S, E>
	where E : ILinkable<Ulid>
	{
		public virtual bool TryLink(E entity)
		{
			if (entity.LinkID.Equals(Ulid.Empty)) entity.LinkID = Ulid.NewUlid();

			return Registry.TryAdd(entity.LinkID, entity);
		}

		public virtual bool TryDisconnect(Ulid linkID, out E disconnectedEntity)
		{
			return Registry.Remove(linkID, out disconnectedEntity);
		}

		public virtual void DisconnectAll(bool trimRegistry)
		{
			ClearRegistry(trimRegistry);
		}
	}

	public abstract class Weaver<S, E> : Register<S, E>
	where S : Weaver<S, E>
	where E : IDiscardable, ILinkable<Ulid>
	{
		public virtual bool TrySever(Ulid linkID)
		{
			bool severed = Registry.Remove(linkID, out var handle);

			if (severed) handle?.Discard();

			return severed;
		}

		public virtual void SeverAll()
		{
			foreach (var id in Registry.Keys) Registry[id]?.Discard();

			ClearRegistry();
		}
	}

	public abstract class UnityWeaver<S, E> : Weaver<S, E>, IUnityWeaver<S, E>
	where S : UnityWeaver<S, E>
	where E : UnityEngine.Object, IDiscardable, ILinkable<Ulid>
	{
		public virtual E Weave(E original)
		{
			static E CreateNewInstance(ref E original)
			{
				if (original is LinkableAsset asset)
					return asset.Clone() as E;
				else
				{
					var newInstance = Instantiate(original);
					newInstance.name = original.name;
					newInstance.LinkID = Ulid.NewUlid();

					return newInstance;
				}
			}

			if (original is IThreadlinkSingleton)
			{
				if (Threadlink.TryGetConstantSingletonID(original.name, out var id))
				{
					if (TryGetLinkedEntity(id, out var singleton) && singleton != null) return singleton;
					else throw new CorruptConstantsBufferException(Scribe.FromSubsystem<S>("Constant IDs Buffer mismatch detected!").ToString());
				}
				else
				{
					var singleton = CreateNewInstance(ref original);

					Threadlink.RegisterConstantSingletonID(singleton.name, singleton.LinkID);
					Registry.Add(singleton.LinkID, singleton);

					if (singleton is not ThreadlinkSubsystem) DontDestroyOnLoad(singleton);
					return singleton;
				}
			}
			else
			{
				var entity = CreateNewInstance(ref original);
				Registry.Add(entity.LinkID, entity);

				return entity;
			}
		}
	}

	public abstract class NativeWeaver<S, E> : Weaver<S, E>, INativeWeaver<S, E>
	where S : NativeWeaver<S, E>
	where E : IDiscardable, ILinkable<Ulid>, new()
	{
		public virtual E Weave()
		{
			static E CreateNewInstance() => new() { LinkID = Ulid.NewUlid() };

			var type = typeof(E);

			if (typeof(IThreadlinkSingleton).IsAssignableFrom(type))
			{
				if (Threadlink.TryGetConstantSingletonID(type.Name, out var id))
				{
					if (TryGetLinkedEntity(id, out var singleton) && singleton.Equals(default) == false) return singleton;
					else throw new CorruptConstantsBufferException(Scribe.FromSubsystem<S>("Constant IDs Buffer mismatch detected!").ToString());
				}
				else
				{
					var singleton = CreateNewInstance();

					Threadlink.RegisterConstantSingletonID(type.Name, singleton.LinkID);
					Registry.Add(singleton.LinkID, singleton);

					(singleton as IThreadlinkSingleton).Boot();

					return singleton;
				}
			}
			else
			{
				var entity = CreateNewInstance();
				Registry.Add(entity.LinkID, entity);

				return entity;
			}
		}
	}
}
