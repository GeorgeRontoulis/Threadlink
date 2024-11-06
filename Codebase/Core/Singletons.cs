namespace Threadlink.Core
{
	using Systems;
	using Utilities.Events;

	public interface IThreadlinkSingleton : IBootable { }
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

		public override Empty Discard(Empty _ = default)
		{
			Instance = null;
			return base.Discard(_);
		}

		public virtual void Boot()
		{
			var thisEntity = this as T;

			if (Instance == null || Instance.Equals(thisEntity) == false) Instance = thisEntity;
			else this.LogException<ExistingSingletonException>();
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

		public override Empty Discard(Empty _ = default)
		{
			Instance = null;
			return base.Discard(_);
		}

		public virtual void Boot()
		{
			var thisEntity = this as T;

			if (Instance == null || Instance.Equals(thisEntity) == false) Instance = thisEntity;
			else this.LogException<ExistingSingletonException>();
		}
	}
}