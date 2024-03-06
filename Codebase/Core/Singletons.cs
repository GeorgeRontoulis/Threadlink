namespace Threadlink.Core
{
	/// <summary>
	/// Base class used to define a Threadlink-Compatible Component that 
	/// only lives as a singular instance during Threadlink's runtime.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LinkableBehaviourSingleton<T> : LinkableBehaviour where T : LinkableBehaviourSingleton<T>
	{
		public static T Instance { get; protected set; }
	}

	/// <summary>
	/// Base class used to define a Threadlink-Compatible Asset that 
	/// only lives as a singular instance during Threadlink's runtime.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LinkableAssetSingleton<T> : LinkableAsset where T : LinkableAssetSingleton<T>
	{
		public static T Instance { get; protected set; }
	}
}