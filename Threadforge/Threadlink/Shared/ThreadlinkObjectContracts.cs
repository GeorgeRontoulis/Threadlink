namespace Threadlink.Shared
{
    /// <summary>
    /// Contract ensuring the existence of a name for use in collections.
    /// </summary>
    public interface INamable { public string Name { get; } }

    /// <summary>
    /// Contract ensuring the existence of a numeric ID for use in collections.
    /// </summary>
    public interface IIdentifiable { public int ID { get; } }

    /// <summary>
    /// Objects implementing this interface are marked as discoverable by Threadlink's initialization pipeline,
    /// and are therefore automatically sequenced for initialization when deploying the core, or loading new scenes.
    /// The active state of the component is also taken into account when scanning for discoverables. Disabled objects will be skipped.
    /// </summary>
    public interface IDiscoverable { }

    /// <summary>
    /// Objects implementing this interface become bootable. 
    /// The bootup step comes first in Threadlink's initialization pipeline.
    /// </summary>
    public interface IBootable
    {
        /// <summary>
        /// Boot = Unity's Awake. Used to set up internal references.
        /// IMPORTANT: The bootup step happens asynchronously and the object order is NOT deterministic.
        /// Keep your <see cref="Boot"/> methods self-contained to avoid race conditions.
        /// </summary>
        public void Boot();
    }

    /// <summary>
    /// Objects implementing this interface become initializable. 
    /// The initialization step comes second in Threadlink's initialization pipeline.
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// Initialize = Unity's Start. Used to set up cross-references between objects.
        /// IMPORTANT: The initialization step happens asynchronously and the object order is NOT deterministic.
        /// Be mindful of how you initialize your objects to avoid race conditions.
        /// </summary>
        public void Initialize();
    }

    /// <summary>
    /// Base interface for Threadlink-Compatible objects that can be disposed of at runtime.
    /// </summary>
    public interface IDiscardable
    {
        /// <summary>
        /// Nullifies all properties and fields of this <see cref="IDiscardable"/> and destroys it.
        /// </summary>
        public void Discard();
    }

    /// <summary>
    /// Base interface for implementing Threadlink-Compatible singletons.
    /// </summary>
    public interface IThreadlinkSingleton : IBootable, IDiscardable { }

    /// <summary>
    /// Base interface for implementing Threadlink-Compatible singletons.
    /// </summary>
    /// <typeparam name="T">The singleton type.</typeparam>
    public interface IThreadlinkSingleton<T> : IThreadlinkSingleton
    {
        public static T Instance { get; }
    }
}