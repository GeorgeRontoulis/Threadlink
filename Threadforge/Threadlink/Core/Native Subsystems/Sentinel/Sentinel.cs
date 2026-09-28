namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System.Runtime.CompilerServices;
    using Cysharp.Threading.Tasks;
    using Generated;
    using Iris;
    using Scribe;
    using Shared;
    using NativeResources = Generated.ThreadlinkIDs.Addressables.NativeResources;

    /// <summary>
    /// Threadlink's platform-services abstraction.
    ///
    /// Unity determines the platform. SentinelConfig only disambiguates distribution
    /// where the target itself is insufficient (for example Windows storefronts).
    /// Runtime modules register exact Platform / Distribution implementations in code.
    ///
    /// Every time a deployment settles, Ready or Failed, Sentinel publishes the Iris event
    /// OnSentinelStateChanged (Action&lt;Sentinel&gt;): after the first deployment, after
    /// <see cref="RetryAsync"/>, and when a Ready platform is lost (for example the Steam client
    /// shutting down), in which case Sentinel moves to Failed with <see cref="SentinelError.PlatformLost"/>.
    /// </summary>
    public sealed class Sentinel
        : ThreadlinkSubsystem<Sentinel>,
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
            Failed,
        }

        public DeploymentState State { get; private set; } = DeploymentState.Uninitialized;

        public SentinelResult InitializationResult { get; private set; } =
            SentinelResult.Failure(SentinelError.NotInitialized);
        public SentinelPlatformMarker ActivePlatform { get; private set; } =
            SentinelPlatformMarker.Unknown;
        public SentinelDistribution ActiveDistribution { get; private set; } =
            SentinelDistribution.None;

        public string ActiveModuleID { get; private set; }
        public string ActiveModuleDisplayName { get; private set; }

        public ISentinelPlatform Platform { get; private set; }

        public SentinelCapability Capabilities =>
            Platform != null ? Platform.Capabilities : SentinelCapability.None;

        private SentinelConfig Config { get; set; }
        private bool Retrying { get; set; }

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return Fail(SentinelError.NotInitialized, "Threadlink Core is unavailable.");

            const NativeResources ID = NativeResources.SentinelConfig;

            if (
                !TryConsumeDependency(
                    await core.NativeConfig.LoadNativeResourceAsync<SentinelConfig>(ID)
                )
            )
            {
                return Fail(
                    SentinelError.NotFound,
                    "SentinelConfig could not be loaded from Threadlink Native Config."
                );
            }

            // Platform availability is an environmental condition rather than a missing dependency.
            // A failed platform leaves Sentinel in DeploymentState.Failed, reported through State and
            // InitializationResult, without aborting the deployment of Threadlink as a whole.
            await TryDeployPlatformAsync();
            PublishStateChanged();
            return true;
        }

        /// <summary>
        /// Deploy the platform again after it failed or was lost, for example once the player has started
        /// Steam. Returns whether Sentinel is Ready. Publishes OnSentinelStateChanged when it settles.
        /// </summary>
        public async UniTask<bool> RetryAsync()
        {
            if (State is DeploymentState.Ready)
                return true;

            if (State is not DeploymentState.Failed || Config == null || Retrying)
                return false;

            Retrying = true;

            try
            {
                // A lost platform may still await its discard.
                SafeDiscardPlatform();

                bool ready = await TryDeployPlatformAsync();

                // Discarded while deploying (the application quit): leave the state Discard set.
                if (Config == null)
                {
                    SafeDiscardPlatform();
                    return false;
                }

                PublishStateChanged();
                return ready;
            }
            finally
            {
                Retrying = false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Development only: act as if the Ready platform had just been lost, through the same path a
        /// module's report takes. For tests of what the game does when, for example, Steam closes.
        /// </summary>
        public void SimulatePlatformLoss()
        {
            OnPlatformLost(SentinelResult.Failure(SentinelError.PlatformLost, "Simulated platform loss."));
        }
#endif

        private async UniTask<bool> TryDeployPlatformAsync()
        {
            State = DeploymentState.ResolvingPlatform;

            ActivePlatform = SentinelRuntimePlatformResolver.Resolve();

            if (ActivePlatform is SentinelPlatformMarker.Unknown)
            {
                return Fail(
                    SentinelError.Unsupported,
                    $"Sentinel does not recognize Unity runtime platform '{UnityEngine.Application.platform}'."
                );
            }

            ActiveDistribution = ResolveDistribution();

            if (ActiveDistribution is SentinelDistribution.None)
            {
                return Fail(
                    SentinelError.InvalidArgument,
                    $"SentinelConfig contains no valid distribution for '{ActivePlatform}'."
                );
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
                    return Fail(
                        SentinelError.NativeFailure,
                        $"Sentinel module '{module.ModuleID}' returned a null platform."
                    );
                }

                Platform.Lost += OnPlatformLost;
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
                        "]."
                    )
                    .ToUnityConsole();
            }
            else
            {
                this.Send(
                        "Sentinel platform deployment failed; platform services are unavailable: ",
                        InitializationResult.ToString()
                    )
                    .ToUnityConsole(DebugType.Error);
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
        public bool HasCapability(SentinelCapability capability) =>
            State is DeploymentState.Ready && Capabilities.Has(capability);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetService<T>(out T service)
            where T : class, ISentinelService
        {
            if (State is DeploymentState.Ready && Platform != null)
                return Platform.TryGetService(out service);

            service = null;
            return false;
        }

        private SentinelDistribution ResolveDistribution()
        {
            var configured = Config.GetDistribution(ActivePlatform);

            if (!SentinelDistributionOverride.TryResolve(out var requested, out var source))
                return configured;

            if (SentinelDistributionPolicy.IsAllowed(ActivePlatform, requested))
            {
                this.Send(
                        "Distribution overridden from ",
                        configured.ToString(),
                        " to ",
                        requested.ToString(),
                        " by ",
                        source,
                        "."
                    )
                    .ToUnityConsole();

                return requested;
            }

            this.Send(
                    requested.ToString(),
                    " is not allowed on ",
                    ActivePlatform.ToString(),
                    ". Ignoring the override requested by ",
                    source,
                    "."
                )
                .ToUnityConsole(DebugType.Warning);

            return configured;
        }

        /// <summary>
        /// A Ready platform went away. Everything using it hears OnSentinelStateChanged first and releases
        /// it; the platform itself is discarded a frame later, once the platform callback that reported
        /// the loss has returned (Steam's API must not shut down inside its own callback dispatch).
        /// </summary>
        private void OnPlatformLost(SentinelResult reason)
        {
            if (State is not DeploymentState.Ready)
                return;

            InitializationResult = reason;
            State = DeploymentState.Failed;

            this.Send("Sentinel lost its platform; platform services are unavailable: ", reason.ToString())
                .ToUnityConsole(DebugType.Warning);

            PublishStateChanged();
            DiscardLostPlatformAsync(Platform).Forget();
        }

        private async UniTaskVoid DiscardLostPlatformAsync(ISentinelPlatform lost)
        {
            await UniTask.Yield();

            // Retried, or Sentinel discarded, meanwhile: that already discarded it.
            if (lost != null && Platform == lost && State is DeploymentState.Failed)
                SafeDiscardPlatform();
        }

        private void PublishStateChanged() => Iris.Publish(ThreadlinkIDs.Iris.Events.OnSentinelStateChanged, this);

        private void SafeDiscardPlatform()
        {
            if (Platform != null)
                Platform.Lost -= OnPlatformLost;

            try
            {
                Platform?.Discard();
            }
            catch (System.Exception exception)
            {
                this.Send("Exception while discarding Sentinel platform: ", exception.Message)
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
