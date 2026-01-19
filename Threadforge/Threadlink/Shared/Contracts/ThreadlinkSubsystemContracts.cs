namespace Threadlink.Shared
{
    using Cysharp.Threading.Tasks;

    /// <summary>
    /// Base interface for a Threadlink Subsystem.
    /// You are strongly adviced to use <see cref="IThreadlinkSubsystem{Singleton}"/> instead.
    /// </summary>
    public interface IThreadlinkSubsystem : IIdentifiable, IThreadlinkSingleton { }

    /// <summary>
    /// Base interface for a Threadlink Subsystem with a singleton.
    /// </summary>
    /// <typeparam name="Singleton">The singleton type.</typeparam>
    public interface IThreadlinkSubsystem<Singleton> : IThreadlinkSingleton<Singleton>, IThreadlinkSubsystem { }

    /// <summary>
    /// Base interface for objects that rely on, and must therefore consume some sort of dependecy.
    /// </summary>
    /// <typeparam name="Dependency">The type of the dependency.</typeparam>
    public interface IDependencyConsumer<Dependency>
    {
        public bool TryConsumeDependency(Dependency input);
    }

    /// <summary>
    /// Base interface for objects that should preload resources.
    /// Injected in Threadlink's deployment and object initialization steps
    /// </summary>
    public interface IAddressablesPreloader
    {
        public UniTask<bool> TryPreloadAssetsAsync();
    }
}
