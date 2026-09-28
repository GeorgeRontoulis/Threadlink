namespace Threadlink.Core
{
    using global::Threadlink.Shared;
    using System.Runtime.CompilerServices;
    using Unity.Scripting.LifecycleManagement;

    /// <summary>
    /// Base class used to define a Threadlink-Compatible Component that
    /// only lives as a singular instance during Threadlink's runtime.
    /// </summary>
    /// <typeparam name="T">The singleton type.</typeparam>
    public abstract partial class LinkableBehaviourSingleton<T> : LinkableBehaviour, IThreadlinkSingleton<T>
    where T : LinkableBehaviour
    {
        /// <summary>
        /// Reset when Play Mode starts or ends without a domain reload: a scene object is destroyed with Play Mode
        /// whether or not it was discarded.
        /// </summary>
        [AutoStaticsCleanup]
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
    public abstract partial class LinkableAssetSingleton<T> : LinkableAsset, IThreadlinkSingleton<T>
    where T : LinkableAsset
    {
        /// <summary>
        /// Reset when Play Mode starts or ends without a domain reload, so no session sees the previous one's instance.
        /// </summary>
        [AutoStaticsCleanup]
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