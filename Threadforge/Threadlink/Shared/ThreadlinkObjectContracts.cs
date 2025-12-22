namespace Threadlink.Shared
{
    using Cysharp.Threading.Tasks;

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

    /// <summary>
    /// Base interface for objects meant to asynchronously consume serialized binary data during a loading or initialization step.
    /// How that data is deserialized or consumed is up to the user.
    /// </summary>
    public interface IAsyncBinaryConsumer
    {
        /// <summary>
        /// Asynchronously process any binary files the consumer depends on.
        /// </summary>
        /// <returns></returns>
        public UniTask ConsumeBinariesAsync();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-Only interface for objects containing authoring data which can then be serialized into binary and stored inside the project.
    /// The binary file can then be loaded on any platform through the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
    /// Use this in conjunction with the <see cref="UnityEngine.ContextMenu"/> attribute, 
    /// Odin or some other custom-drawn button to easily call <see cref="SerializeAuthoringDataIntoBinary"/> from your inspector.
    /// </summary>
    public interface IBinaryAuthor
    {
        /// <summary>
        /// Serialize the author's data into binary and store the resulting file(s) inside the project.
        /// </summary>
        public void SerializeAuthoringDataIntoBinary();
    }
#endif
}