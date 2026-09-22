namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using System.Runtime.CompilerServices;
    using NativeResources = Generated.ThreadlinkIDs.Addressables.NativeResources;

    /// <summary>
    /// Threadlink's platform-services abstraction.
    ///
    /// Unity determines the platform. SentinelConfig only disambiguates distribution
    /// where the target itself is insufficient (for example Windows storefronts).
    /// Runtime modules register exact Platform / Distribution implementations in code.
    /// </summary>
    public sealed class Sentinel : ThreadlinkSubsystem<Sentinel>,
    IDependencyConsumer<SentinelConfig>,
    IAddressablesPreloader
    {
        public enum DeploymentState : byte
        {
            Uninitialized = 0,
            ResolvingPlatform,
            ResolvingModule,
            InitializingPlatform,
            Ready,
            Failed
        }

        public DeploymentState State { get; private set; } = DeploymentState.Uninitialized;

        public SentinelResult InitializationResult { get; private set; } = SentinelResult.Failure(SentinelError.NotInitialized);
        public SentinelPlatformMarker ActivePlatform { get; private set; } = SentinelPlatformMarker.Unknown;
        public SentinelDistribution ActiveDistribution { get; private set; } = SentinelDistribution.None;

        public string ActiveModuleID { get; private set; }
        public string ActiveModuleDisplayName { get; private set; }

        public ISentinelPlatform Platform { get; private set; }

        public SentinelCapability Capabilities => Platform != null ? Platform.Capabilities : SentinelCapability.None;

        private SentinelConfig Config { get; set; }

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return Fail(SentinelError.NotInitialized, "Threadlink Core is unavailable.");

            const NativeResources ID = NativeResources.SentinelConfig;

            if (!TryConsumeDependency(await core.NativeConfig.LoadNativeResourceAsync<SentinelConfig>(ID)))
            {
                return Fail(SentinelError.NotFound, "SentinelConfig could not be loaded from Threadlink Native Config.");
            }

            State = DeploymentState.ResolvingPlatform;

            ActivePlatform = SentinelRuntimePlatformResolver.Resolve();

            if (ActivePlatform is SentinelPlatformMarker.Unknown)
            {
                return Fail(SentinelError.Unsupported,
                $"Sentinel does not recognize Unity runtime platform '{UnityEngine.Application.platform}'.");
            }

            ActiveDistribution = Config.GetDistribution(ActivePlatform);

            if (ActiveDistribution is SentinelDistribution.None)
            {
                return Fail(SentinelError.InvalidArgument,
                $"SentinelConfig contains no valid distribution for '{ActivePlatform}'.");
            }

            State = DeploymentState.ResolvingModule;

            var resolution = SentinelModuleRegistry.Resolve(ActivePlatform, ActiveDistribution);

            if (!resolution.Succeeded)
                return Fail(resolution.Error, resolution.Message);

            var module = resolution.Value;

            ActiveModuleID = module.ModuleID;
            ActiveModuleDisplayName = module.DisplayName;

            try
            {
                Platform = module.CreatePlatform();

                if (Platform == null)
                {
                    return Fail(SentinelError.NativeFailure, $"Sentinel module '{module.ModuleID}' returned a null platform.");
                }

                State = DeploymentState.InitializingPlatform;
                InitializationResult = await Platform.InitializeAsync();

                if (!InitializationResult.Succeeded)
                {
                    SafeDiscardPlatform();
                    State = DeploymentState.Failed;
                    return false;
                }

                State = DeploymentState.Ready;
                return true;
            }
            catch (System.Exception exception)
            {
                SafeDiscardPlatform();
                return Fail(SentinelError.NativeFailure, exception.Message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(SentinelConfig input) => (Config = input) != null;

        public override void Boot()
        {
            base.Boot();

            if (State is DeploymentState.Ready)
            {
                this.Send(
                    "Sentinel deployed ",
                    ActivePlatform.ToString(),
                    " / ",
                    ActiveDistribution.ToString(),
                    " through ",
                    ActiveModuleDisplayName,
                    " [",
                    ActiveModuleID,
                    "].")
                    .ToUnityConsole();
            }
            else
            {
                this.Send("Sentinel platform deployment failed: ", InitializationResult.ToString()).ToUnityConsole(DebugType.Error);
            }
        }

        public override void Discard()
        {
            SafeDiscardPlatform();

            Config = null;

            ActivePlatform = SentinelPlatformMarker.Unknown;
            ActiveDistribution = SentinelDistribution.None;
            ActiveModuleID = null;
            ActiveModuleDisplayName = null;

            State = DeploymentState.Uninitialized;
            InitializationResult = SentinelResult.Failure(SentinelError.NotInitialized);

            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasCapability(SentinelCapability capability) => State is DeploymentState.Ready && Capabilities.Has(capability);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetService<T>(out T service) where T : class, ISentinelService
        {
            if (State is DeploymentState.Ready && Platform != null)
                return Platform.TryGetService(out service);

            service = null;
            return false;
        }

        private void SafeDiscardPlatform()
        {
            try
            {
                Platform?.Discard();
            }
            catch (System.Exception exception)
            {
                this.Send(
                    "Exception while discarding Sentinel platform: ",
                    exception.Message)
                    .ToUnityConsole(DebugType.Warning);
            }
            finally
            {
                Platform = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Fail(SentinelError error, string message)
        {
            InitializationResult = SentinelResult.Failure(error, message);
            State = DeploymentState.Failed;
            return false;
        }
    }
}
