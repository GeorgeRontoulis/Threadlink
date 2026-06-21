namespace Threadlink.Core
{
    using Shared;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Base class used to define a Threadlink-Compatible Component that 
    /// only lives as a singular instance during Threadlink's runtime.
    /// </summary>
    /// <typeparam name="T">The singleton type.</typeparam>
    public abstract class LinkableBehaviourSingleton<T> : LinkableBehaviour, IThreadlinkSingleton<T>
    where T : LinkableBehaviour
    {
        protected static T Instance { get; set; }

        public override void Discard()
        {
            Instance = null;
            base.Discard();
        }

        public virtual void Boot() => Instance = this as T;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSingleton(out T result) => (result = Instance) != null;
    }

    /// <summary>
    /// Base class used to define a Threadlink-Compatible Asset that 
    /// only lives as a singular instance during Threadlink's runtime.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LinkableAssetSingleton<T> : LinkableAsset, IThreadlinkSingleton<T>
    where T : LinkableAsset
    {
        protected static T Instance { get; set; }

        public override void Discard()
        {
            Instance = null;
            base.Discard();
        }

        public virtual void Boot() => Instance = this as T;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSingleton(out T result) => (result = Instance) != null;
    }
}