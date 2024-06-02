namespace Threadlink.Core
{
	using System.Collections.Generic;
	using Systems;
	using UnityEngine;
	using Utilities.Collections;

	/// <summary>
	/// Base class used to define a Threadlink Sub-System.
	/// Derive from this class only if you  need a Sub-System that explicitly manages its own collection of objects.
	/// If your Sub-System is not supposed to manage live objects, use one of the Singleton Types instead.
	/// </summary>
	public abstract class ThreadlinkSystem<SingletonType, ManagedType> : LinkableBehaviourSingleton<SingletonType>
	where SingletonType : LinkableBehaviourSingleton<SingletonType>
	where ManagedType : Object, ILinkable
	{
		protected int LinkedEntityCount => LinkedEntities.Count;

		protected bool EntityListAlteredSinceLastSort { get; private set; }
		protected List<ManagedType> LinkedEntities { get; private set; }

		public override void Discard()
		{
			LinkedEntities = null;
			Instance = null;
			base.Discard();
		}

		public override void Boot()
		{
			LinkedEntities = new();
			EntityListAlteredSinceLastSort = true;
		}

		private void ClearManagedEntitiesList()
		{
			LinkedEntities?.Clear();
			LinkedEntities?.TrimExcess();
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
				catch (System.Exception exception)
				{
					Scribe.SystemLog(LinkID, Scribe.ErrorNotif, exception.Message);
				}
			}

			ClearManagedEntitiesList();
		}

		public virtual ManagedType FindManagedEntity(string entityLinkID)
		{
			if (EntityListAlteredSinceLastSort)
			{
				LinkedEntities.SortByID();
				EntityListAlteredSinceLastSort = false;
			}

			return LinkedEntities.BinarySearch(entityLinkID);
		}

		/// <summary>
		/// Create a copy of the original <typeparamref name="Entity"/> provided and link it to <typeparamref name="SingletonType"/>.
		/// </summary>
		/// <typeparam name="Entity"> The type of <typeparamref name="ILinkable"/> to create and link.</typeparam>
		/// <param name="original">The original <typeparamref name="Entity"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		/// <returns>The created and linked <typeparamref name="Entity"/>.</returns>
		public virtual Entity Weave<Entity>(Entity original, bool logAction = false) where Entity : ManagedType
		{
			if (original == null)
			{
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "The requested Entity to weave is NULL!");
				return null;
			}

			var copy = Instantiate(original);
			LinkedEntities.Add(copy);

			copy.name = original.name;

			if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Weaved Entity ", copy.name, ".");

			EntityListAlteredSinceLastSort = true;
			return copy;
		}

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
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "The requested Entity to link is NULL!");
				return null;
			}
			else if (LinkedEntities.Contains(instance))
			{
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "The requested Entity to link is already linked! Discarding the duplicate Entity!");
				instance.Discard();
				return null;
			}

			LinkedEntities.Add(instance);

			if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Linked Entity ", instance.name, ".");

			EntityListAlteredSinceLastSort = true;
			return instance;
		}

		/// <summary>
		/// Discard a created <typeparamref name="ManagedType"/> from <typeparamref name="SingletonType"/>.
		/// </summary>
		/// <param name="instance">The created <typeparamref name="ManagedType"/>.</param>
		/// <param name="logAction">Whether to provide console logs of the process.</param>
		public virtual void Sever(ManagedType entity, bool logAction = false)
		{
			if (entity == null)
			{
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "The requested Entity to sever is NULL!");
				return;
			}

			void DiscardTargetEntity() { entity.Discard(); }

			if (LinkedEntities.Contains(entity))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(entity));
				DiscardTargetEntity();
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Severed Entity ", entity.name, ".");
			}
			else
			{
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "A Sever request was made for Entity ", entity.name,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen! Discarding the Entity...");

				DiscardTargetEntity();
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
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "The requested Entity to disconnect is NULL!");
				return;
			}

			if (LinkedEntities.Contains(instance))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(instance));
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, Scribe.InfoNotif, "Disconnected Entity ", instance.name, ".");
			}
			else
			{
				Scribe.SystemLog(LinkID, Scribe.ErrorNotif, "A Disconnect request was made for Entity ", instance.name,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen! Discarding the Entity...");

				instance.Discard();
			}
		}
	}
}
