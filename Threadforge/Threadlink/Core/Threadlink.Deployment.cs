namespace Threadlink.Core
{
    using Cysharp.Text;
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Initium;
    using NativeSubsystems.Iris;
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;

    public partial class Threadlink
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async UniTaskVoid DeployCoreAsync()
        {
            await Addressables.InitializeAsync().ToUniTask();

            var nativeConfigHandle = Addressables.LoadAssetAsync<ThreadlinkNativeConfig>(NativeConstants.Addressables.NATIVE_CONFIG);

            await nativeConfigHandle.ToUniTask();

            if (nativeConfigHandle.Status is AsyncOperationStatus.Succeeded)
            {
                var nativeConfig = nativeConfigHandle.Result;
                var userConfig = await nativeConfig.LoadUserConfigAsync();

                if (userConfig != null)
                {
                    if (userConfig.CoreDeployment is ThreadlinkUserConfig.CoreDeploymentMethod.Automatic)
                    {
                        var core = new Threadlink
                        {
                            NativeConfig = nativeConfig,
                            UserConfig = userConfig
                        };

                        await core.DeployAsync();

                        Initium.BootAndInitUnityObjectsAsync().Forget();
                    }
                    else
                    {
                        if (nativeConfigHandle.IsValid())
                            nativeConfigHandle.Release();

                        Scribe.Send<Threadlink>("Core Deployment is set to Manual. You are responsible for deploying the Core!").ToUnityConsole(DebugType.Warning);
                    }
                }
                else Scribe.Send<Threadlink>("Could not load User Config! Will not deploy the Core!").ToUnityConsole(DebugType.Error);
            }
            else
            {
                if (nativeConfigHandle.IsValid())
                    nativeConfigHandle.Release();

                Scribe.Send<Threadlink>("Could not load Native Config! Will not deploy the Core!").ToUnityConsole(DebugType.Error);
            }
        }

        internal async UniTask DeployAsync()
        {
            #region Local Helper Methods:
            static async UniTask AwaitAll(List<UniTask> tasks, bool trim = false)
            {
                await UniTask.WhenAll(tasks);
                tasks.Clear();

                if (trim)
                    tasks.TrimExcess();
            }

            static string BuildWovenSubsystemsReport(IThreadlinkSubsystem[] wovenSubsystems)
            {
                using var sb = ZString.CreateUtf8StringBuilder();
                var newline = Environment.NewLine;
                int length = wovenSubsystems.Length;
                int lastIndex = length - 1;
                IThreadlinkSubsystem subsystem;

                sb.Append(newline);

                for (int i = 0; i < length; i++)
                {
                    subsystem = wovenSubsystems[i];

                    if (subsystem == null)
                        continue;

                    sb.Append(subsystem.GetType().Name);

                    if (i < lastIndex)
                        sb.Append(newline);
                }

                return sb.ToString();
            }
            #endregion

            Boot();

            var wovenSubsystems = Iris.Publish<IThreadlinkSubsystem[]>(Iris.Events.OnSubsystemRegistration);
            var tasks = new List<UniTask>(wovenSubsystems.Length);

            var preloaders = wovenSubsystems.OfType<IAddressablesPreloader>();

            foreach (var preloader in preloaders)
                tasks.Add(preloader.TryPreloadAssetsAsync());

            await AwaitAll(tasks);

            foreach (var system in wovenSubsystems)
                tasks.Add(Initium.BootAsync(system));

            await AwaitAll(tasks);

            var initializableSystems = wovenSubsystems.OfType<IInitializable>();

            foreach (var system in initializableSystems)
                tasks.Add(Initium.InitializeAsync(system));

            await AwaitAll(tasks, true);

            this.Send("Core successfully deployed. All Subsystems operational. Woven Systems: ", BuildWovenSubsystemsReport(wovenSubsystems)).ToUnityConsole();
        }
    }
}
