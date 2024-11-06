namespace Threadlink.Core
{
	using MassTransit;
	using Sirenix.OdinInspector;
	using System.Collections.Generic;
	using Systems;
	using Utilities.Events;

	public interface IThreadlinkSystem : ILinkable { }
	public interface IThreadlinkSystem<S> : IThreadlinkSystem, IThreadlinkSingleton<S> { }
	public interface IUnityWeaver<S, E> : IThreadlinkSystem<S> { public E Weave(E original); }
	public interface INativeWeaver<S, E> : IThreadlinkSystem<S> { public E Weave(); }

	public abstract class ThreadlinkSystem : LinkableBehaviour, IThreadlinkSystem, IBootable
	{
		public abstract void Boot();
	}

	public abstract class ThreadlinkSystem<S> : ThreadlinkSystem, IThreadlinkSystem<S>
	where S : ThreadlinkSystem<S>
	{
		public static S Instance { get; protected set; }

		public override void Boot()
		{
			var thisSystem = this as S;

			if (Instance == null || Instance.Equals(thisSystem) == false) Instance = thisSystem;
			else this.LogException<ExistingSingletonException>();
		}

		public override Empty Discard(Empty _ = default)
		{
			Instance = null;
			return base.Discard(_);
		}
	}

	public abstract class Register<S, E> : ThreadlinkSystem<S>
	where S : Register<S, E>
	where E : ILinkable
	{
#if ODIN_INSPECTOR
		[ShowInInspector, ReadOnly]
#endif
		protected Dictionary<NewId, E> Registry { get; private set; }

		protected void ClearRegistry(bool trimRegistry = false)
		{
			Registry.Clear();
			if (trimRegistry) Registry.TrimExcess();
		}

		public override Empty Discard(Empty _ = default)
		{
			ClearRegistry(true);
			Registry = null;
			return base.Discard(_);
		}

		public override void Boot()
		{
			Registry = new();
			base.Boot();
		}

		public bool TryGetLinkedEntity(NewId entityID, out E entity)
		{
			return Registry.TryGetValue(entityID, out entity);
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
	where E : ILinkable
	{
		public virtual bool TryLink(E entity)
		{
			if (entity.InstanceID.Equals(NewId.Empty))
				entity.InstanceID = NewId.Next();

			return Registry.TryAdd(entity.InstanceID, entity);
		}

		public virtual bool TryDisconnect(NewId entityID, out E disconnectedEntity)
		{
			return Registry.Remove(entityID, out disconnectedEntity);
		}

		public virtual void DisconnectAll(bool trimRegistry)
		{
			ClearRegistry(trimRegistry);
		}
	}

	public abstract class Weaver<S, E> : Register<S, E>
	where S : Weaver<S, E>
	where E : IDiscardable
	{
		public virtual bool TrySever(NewId entityID)
		{
			bool severed = Registry.Remove(entityID, out var handle);

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
	where E : UnityEngine.Object, IDiscardable
	{
		private E CreateNewInstance(ref E original)
		{
			var singleton = Instantiate(original);
			singleton.name = original.name;
			singleton.InstanceID = NewId.Next();
			return singleton;
		}

		public virtual E Weave(E original)
		{
			if (original is IThreadlinkSingleton)
			{
				if (Threadlink.TryGetConstantSingletonID(original.LinkID, out var id))
				{
					if (TryGetLinkedEntity(id, out var singleton) && singleton != null) return singleton;
					else
					{
						this.SystemLog<ConstantIDsBufferException>();
						return null;
					}
				}
				else
				{
					var singleton = CreateNewInstance(ref original);

					Threadlink.RegisterConstantSingletonID(singleton.LinkID, singleton.InstanceID);
					Registry.Add(singleton.InstanceID, singleton);

					if (singleton is not IThreadlinkSystem) DontDestroyOnLoad(singleton);
					return singleton;
				}
			}
			else
			{
				var entity = CreateNewInstance(ref original);
				Registry.Add(entity.InstanceID, entity);

				return entity;
			}
		}
	}

	public abstract class NativeWeaver<S, E> : Weaver<S, E>, INativeWeaver<S, E>
	where S : NativeWeaver<S, E>
	where E : IDiscardable, new()
	{
		private E CreateNewInstance() { return new E { InstanceID = NewId.Next() }; }

		public virtual E Weave()
		{
			var type = typeof(E);

			if (typeof(IThreadlinkSingleton).IsAssignableFrom(type))
			{
				if (Threadlink.TryGetConstantSingletonID(type.Name, out var id))
				{
					if (TryGetLinkedEntity(id, out var singleton) && singleton.Equals(default) == false) return singleton;
					else
					{
						this.SystemLog<ConstantIDsBufferException>();
						return default;
					}
				}
				else
				{
					var singleton = CreateNewInstance();

					Threadlink.RegisterConstantSingletonID(type.Name, singleton.InstanceID);
					Registry.Add(singleton.InstanceID, singleton);

					(singleton as IThreadlinkSingleton).Boot();

					return singleton;
				}
			}
			else
			{
				var entity = CreateNewInstance();
				Registry.Add(entity.InstanceID, entity);

				return entity;
			}
		}
	}
}
