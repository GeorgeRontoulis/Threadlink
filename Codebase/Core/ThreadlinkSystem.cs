namespace Threadlink.Core
{
	using Cysharp.Threading.Tasks;
	using System;
	using System.Collections.Generic;
	using Systems;
	using Utilities.Collections;
	using Utilities.Events;

	public interface IScriptableWeavingData<T> where T : ILinkable { }
	public struct UnityWeavingData<T> : IScriptableWeavingData<T> where T : ILinkable
	{
		public T Original { get; set; }

		public UnityWeavingData(T original) { Original = original; }
	}
	public struct NativeWeavingData<T> : IScriptableWeavingData<T> where T : ILinkable { }

	public abstract class ThreadlinkSystem<SingletonType, ManagedType, WeavingData> : LinkableBehaviourSingleton<SingletonType>
	where SingletonType : LinkableBehaviourSingleton<SingletonType>
	where ManagedType : ILinkable
	where WeavingData : IScriptableWeavingData<ManagedType>
	{
		protected int LinkedEntityCount => LinkedEntities.Count;

		protected bool EntityListAlteredSinceLastSort { get; set; }
		protected List<ManagedType> LinkedEntities { get; private set; }

		public override VoidOutput Discard(VoidInput _ = default)
		{
			LinkedEntities = null;
			Instance = null;
			return base.Discard(_);
		}

		public override void Boot()
		{
			base.Boot();
			LinkedEntities = new();
			EntityListAlteredSinceLastSort = true;
		}

		private void ClearManagedEntitiesList()
		{
			if (LinkedEntities != null)
			{
				LinkedEntities.Clear();
				LinkedEntities.TrimExcess();
			}

			EntityListAlteredSinceLastSort = true;
			Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Cleared all managed Entities.");
		}

		public virtual void DisconnectAll() { ClearManagedEntitiesList(); }

		public virtual void SeverAll()
		{
			int count = LinkedEntities.Count;
			for (int i = 0; i < count; i++)
			{
				try
				{
					LinkedEntities[i].Discard();
				}
				catch (Exception exception)
				{
					Scribe.SystemLog<InvalidOperationException>(LinkID, exception.Message);
				}
			}

			ClearManagedEntitiesList();
		}

		public void FindManagedEntity(string entityLinkID, out ManagedType result)
		{
			if (EntityListAlteredSinceLastSort)
			{
				LinkedEntities.SortByID();
				EntityListAlteredSinceLastSort = false;
			}

			LinkedEntities.BinarySearch(entityLinkID, out var entity);
			result = entity;
		}

		/// <summary>
		/// Create a copy of the original <typeparamref name="Entity"/> provided and link it to <typeparamref name="SingletonType"/>.
		/// </summary>
		/// <typeparam name="Entity"> The type of <typeparamref name="ILinkable"/> to create and link.</typeparam>
		/// <param name="original">The original <typeparamref name="Entity"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		/// <returns>The created and linked <typeparamref name="Entity"/>.</returns>
		public abstract ManagedType Weave(WeavingData data, bool logAction = false);

		/// <summary>
		/// Link an existing <typeparamref name="Entity"/> to <typeparamref name="SingletonType"/>.
		/// </summary>
		/// <typeparam name="Entity"> The type of <typeparamref name="ILinkable"/> to link.</typeparam>
		/// <param name="instance">The existing <typeparamref name="Entity"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		/// <returns>The linked <typeparamref name="Entity"/>.</returns>
		public virtual Entity Link<Entity>(Entity instance, bool logAction = false) where Entity : ManagedType
		{
			if (instance == null)
			{
				Scribe.SystemLog<ArgumentNullException>(LinkID, "The requested Entity to link is NULL!");
			}
			else if (LinkedEntities.Contains(instance))
			{
				Scribe.SystemLog<InvalidOperationException>(LinkID, "The requested Entity to link is already linked!");
			}

			LinkedEntities.Add(instance);

			if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Linked Entity ", instance.LinkID, ".");

			EntityListAlteredSinceLastSort = true;
			return instance;
		}

		/// <summary>
		/// Discard a created <typeparamref name="ManagedType"/> from <typeparamref name="SingletonType"/>.
		/// </summary>
		/// <param name="instance">The created <typeparamref name="ManagedType"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		public void Sever(ManagedType entity, bool logAction = false)
		{
			if (entity == null)
			{
				Scribe.SystemLog<ArgumentNullException>(LinkID, "The requested Entity to sever is NULL!");
			}

			if (LinkedEntities.Contains(entity))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(entity));
				entity.Discard();
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Severed Entity ", entity.LinkID, ".");
			}
			else
			{
				Scribe.SystemLog<InvalidOperationException>(LinkID, "A Sever request was made for Entity ", entity.LinkID,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen!");
			}
		}

		/// <summary>
		/// Disconnect a linked <typeparamref name="ManagedType"/> from <typeparamref name="SingletonType"/>. 
		/// This will NOT discard the <typeparamref name="ManagedType"/>.
		/// </summary>
		/// <param name="instance">The existing <typeparamref name="ManagedType"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		public virtual void Disconnect(ManagedType instance, bool logAction = false)
		{
			if (instance == null)
			{
				Scribe.SystemLog<ArgumentNullException>(LinkID, "The requested Entity to disconnect is NULL!");
			}

			if (LinkedEntities.Contains(instance))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(instance));
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Disconnected Entity ", instance.LinkID, ".");
			}
			else
			{
				Scribe.SystemLog<InvalidOperationException>(LinkID, "A Disconnect request was made for Entity ", instance.LinkID,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen!");
			}
		}
	}

	/// <summary>
	/// Base class used to define a Threadlink Sub-System that manages Unity Objects.
	/// Derive from this class only if you  need a Sub-System that explicitly manages its own collection of objects.
	/// If your Sub-System is not supposed to manage live objects, use one of the Singleton Types instead.
	/// </summary>
	public abstract class UnitySystem<SingletonType, ManagedType> : ThreadlinkSystem<SingletonType, ManagedType, UnityWeavingData<ManagedType>>
	where SingletonType : LinkableBehaviourSingleton<SingletonType>
	where ManagedType : UnityEngine.Object, ILinkable
	{
		public override ManagedType Weave(UnityWeavingData<ManagedType> data, bool logAction = false)
		{
			if (data.Original == null)
			{
				Scribe.SystemLog<ArgumentNullException>(LinkID, "The requested Entity to weave is NULL!");
			}

			var copy = Instantiate(data.Original);
			LinkedEntities.Add(copy);

			copy.name = data.Original.name;

			if (copy is LinkableAsset) (copy as LinkableAsset).IsInstance = true;

			if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Weaved Entity ", copy.name, ".");

			EntityListAlteredSinceLastSort = true;
			return copy;
		}
	}

	/// <summary>
	/// Base class used to define a Threadlink Sub-System that manages Pure C# Objects.
	/// Derive from this class only if you  need a Sub-System that explicitly manages its own collection of objects.
	/// If your Sub-System is not supposed to manage live objects, use one of the Singleton Types instead.
	/// </summary>
	public abstract class NativeSystem<SingletonType, ManagedType> : ThreadlinkSystem<SingletonType, ManagedType, NativeWeavingData<ManagedType>>
	where SingletonType : LinkableBehaviourSingleton<SingletonType>
	where ManagedType : ILinkable, new()
	{
		public override ManagedType Weave(NativeWeavingData<ManagedType> data = default, bool logAction = false)
		{
			var copy = new ManagedType();
			LinkedEntities.Add(copy);

			if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Weaved Entity ", copy.LinkID, ".");

			EntityListAlteredSinceLastSort = true;
			return copy;
		}
	}
}
