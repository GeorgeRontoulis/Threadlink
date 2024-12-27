namespace Threadlink.Core
{
	using Exceptions;
	using Subsystems.Scribe;

	public interface IThreadlinkSingleton : IBootable, IDiscardable { }
	public interface IThreadlinkSingleton<T> : IThreadlinkSingleton
	{
		public static T Instance { get; }
	}

	/// <summary>
	/// Base class used to define a Threadlink-Compatible Component that 
	/// only lives as a singular instance during Threadlink's runtime.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LinkableBehaviourSingleton<T> : LinkableBehaviour, IThreadlinkSingleton<T>
	where T : LinkableBehaviour
	{
		public static T Instance { get; protected set; }

		public override void Discard()
		{
			Instance = null;
			base.Discard();
		}

		public virtual void Boot()
		{
			var thisEntity = this as T;

			if (Instance == null || Instance.Equals(thisEntity) == false) Instance = thisEntity;
			else throw new ExistingSingletonException(Scribe.FromSubsystem<Threadlink>("This Singleton already exists!").ToString());
		}
	}

	/// <summary>
	/// Base class used to define a Threadlink-Compatible Asset that 
	/// only lives as a singular instance during Threadlink's runtime.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LinkableAssetSingleton<T> : LinkableAsset, IThreadlinkSingleton<T>
	where T : LinkableAsset
	{
		public static T Instance { get; protected set; }

		public override void Discard()
		{
			Instance = null;
			base.Discard();
		}

		public virtual void Boot()
		{
			var thisEntity = this as T;

			if (Instance == null || Instance.Equals(thisEntity) == false) Instance = thisEntity;
			else throw new ExistingSingletonException(Scribe.FromSubsystem<Threadlink>("This Singleton already exists!").ToString());
		}
	}
}