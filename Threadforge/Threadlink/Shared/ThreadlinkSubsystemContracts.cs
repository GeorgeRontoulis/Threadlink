namespace Threadlink.Shared
{
    using Cysharp.Threading.Tasks;

    public interface IThreadlinkSubsystem : IIdentifiable, IThreadlinkSingleton { }
    public interface IThreadlinkSubsystem<Singleton> : IThreadlinkSingleton<Singleton>, IThreadlinkSubsystem { }

    public interface IThreadlinkDependency<Dependency>
    {
        public bool TryConsumeDependency(Dependency input);
    }

    public interface IAddressablesPreloader
    {
        public UniTask<bool> TryPreloadAssetsAsync();
    }
}
