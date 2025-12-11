namespace Threadlink.Core
{
    using Shared;

    /// <summary>
    /// Base class used to define a Threadlink-Compatible Component that 
    /// only lives as a singular instance during Threadlink's runtime.
    /// </summary>
    /// <typeparam name="T">The singleton type.</typeparam>
    public abstract class LinkableBehaviourSingleton<T> : LinkableBehaviour, IThreadlinkSingleton<T>
    where T : LinkableBehaviour
    {
        public static T Instance { get; protected set; }

        public override void Discard()
        {
            Instance = null;
            base.Discard();
        }

        public virtual void Boot() => Instance = this as T;
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

        public virtual void Boot() => Instance = this as T;
    }
}