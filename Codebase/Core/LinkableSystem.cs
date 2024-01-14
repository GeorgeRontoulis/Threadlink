namespace Threadlink.Core
{
	using System.Collections.Generic;
	using Systems;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.UnityLogging;

	public abstract class BaseLinkableSystem : LinkableObject
	{
		protected bool EntityListAlteredSinceLastSort { get; set; }

		[Space(10)]

		[SerializeField] protected bool performBookkeeping = true;

		protected abstract void ClearManagedEntitiesList();

		private void TickSeverance()
		{
			EntityListAlteredSinceLastSort = true;
			Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Severed all managed Entities.");
		}

		public virtual void SeverAllInstances()
		{
			ClearManagedEntitiesList();
			TickSeverance();
		}

		public virtual void SeverAll()
		{
			ClearManagedEntitiesList();
			TickSeverance();
		}
	}

	/// <summary>
	/// Base class for a Threadlink Subsystem.
	/// </summary>
	public abstract class LinkableSystem<T> : BaseLinkableSystem where T : LinkableEntity
	{
		protected int LinkedEntityCount => LinkedEntities.Count;

		protected List<T> LinkedEntities { get; set; }

		public T FindLinkedEntity(string entityLinkID)
		{
			if (EntityListAlteredSinceLastSort)
			{
				LinkedEntities.Sort((T x, T y) => string.Compare(x.LinkID, y.LinkID));
				EntityListAlteredSinceLastSort = false;
			}

			return LinkedEntities.BinarySearch(entityLinkID);
		}

		public override void Boot()
		{
			LinkedEntities = performBookkeeping ? new List<T>() : null;
		}

		public virtual Entity Link<Entity>(Entity original, bool logAction = false) where Entity : T
		{
			if (performBookkeeping == false) return null;

			if (original == null)
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "The requested Entity to link is NULL!");
				return null;
			}

			Entity copy = Instantiate(original);
			LinkedEntities.Add(copy);

			copy.name = original.name;

			if (logAction) Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Linked Entity ", copy.name, ".");

			EntityListAlteredSinceLastSort = true;
			return copy;
		}

		public virtual Entity LinkInstance<Entity>(Entity instance, bool logAction = false) where Entity : T
		{
			if (performBookkeeping == false) return null;

			if (instance == null)
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "The requested Entity to link is NULL!");
				return null;
			}
			else if (LinkedEntities.Contains(instance))
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "The requested Entity to link is already linked! Discarding the duplicate Entity!");
				instance.Discard();
				return null;
			}

			LinkedEntities.Add(instance);

			if (logAction) Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Linked Entity ", instance.name, ".");

			EntityListAlteredSinceLastSort = true;
			return instance;
		}

		public virtual void Sever(T entity, bool logAction = false)
		{
			if (entity == null)
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "The requested Entity to sever is NULL!");
				return;
			}

			void DiscardTargetEntity() { entity.Discard(); }

			if (LinkedEntities.Contains(entity))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(entity));
				DiscardTargetEntity();
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Severed Entity ", entity.name, ".");
			}
			else
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "A Sever request was made for Entity ", entity.name,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen! Discarding the Entity...");

				DiscardTargetEntity();
			}
		}

		public virtual void SeverInstance(T instance, bool logAction = false)
		{
			if (instance == null)
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "The requested Entity to sever is NULL!");
				return;
			}

			void DiscardTargetEntity() { instance.Discard(); }

			if (LinkedEntities.Contains(instance))
			{
				LinkedEntities.RemoveEfficiently(LinkedEntities.IndexOf(instance));
				EntityListAlteredSinceLastSort = true;
				if (logAction) Scribe.SystemLog(LinkID, DebugNotificationType.Info, "Severed Entity ", instance.name, ".");
			}
			else
			{
				Scribe.SystemLog(LinkID, DebugNotificationType.Error, "A Sever request was made for Entity ", instance.name,
				", however it's not managed by ", LinkID, ". This is probably a memory leak and should never happen! Discarding the Entity...");

				DiscardTargetEntity();
			}
		}

		protected override void ClearManagedEntitiesList()
		{
			LinkedEntities.Clear();
			LinkedEntities.TrimExcess();
		}

		public override void SeverAll()
		{
			int count = LinkedEntities.Count;

			for (int i = 0; i < count; i++) LinkedEntities[i].Discard();

			base.SeverAll();
		}
	}
}
