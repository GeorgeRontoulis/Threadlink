namespace Threadlink.Core
{
    using Cysharp.Text;
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Initium;
    using NativeSubsystems.Iris;
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    public partial class Threadlink
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async UniTaskVoid DeployCoreAsync()
        {
            await Addressables.InitializeAsync().ToUniTask(); //This should never fail.

            var nativeConfig = await Addressables.LoadAssetAsync<ThreadlinkNativeConfig>(NativeConstants.Addressables.NATIVE_CONFIG).ToUniTask();

            if (nativeConfig != null)
            {
                var userConfig = await nativeConfig.LoadUserConfigAsync();

                if (userConfig != null)
                {
                    var core = new Threadlink
                    {
                        NativeConfig = nativeConfig,
                        UserConfig = userConfig
                    };

                    await core.DeployAsync();

                    Initium.BootAndInitUnityObjectsAsync().Forget();
                }
                else Scribe.Send<Threadlink>("Could not load User Config! Will not deploy the Core!").ToUnityConsole(DebugType.Error);
            }
            else Scribe.Send<Threadlink>("Could not load Native Config! Will not deploy the Core!").ToUnityConsole(DebugType.Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async UniTask DeployAsync()
        {
            Boot();

            await RegisterSubsystemsAsync(Iris.Events.OnNativeSubsystemRegistration);
            await RegisterSubsystemsAsync(Iris.Events.OnUserSubsystemRegistration);

            this.Send("Core successfully deployed.").ToUnityConsole();
            Iris.Publish(Iris.Events.OnCoreDeployed, this);
        }

        private async UniTask RegisterSubsystemsAsync(Iris.Events subsystemsRegistrationEvent)
        {
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

                    sb.Append(i + 1);
                    sb.Append(". ");
                    sb.Append(subsystem.GetType().Name);

                    if (i < lastIndex)
                        sb.Append(newline);
                }

                return sb.ToString();
            }

            var wovenSubsystems = Iris.Publish<IThreadlinkSubsystem[]>(subsystemsRegistrationEvent);
            int subsystemCount = wovenSubsystems.Length;

            string type = subsystemsRegistrationEvent switch
            {
                Iris.Events.OnNativeSubsystemRegistration => "Native",
                Iris.Events.OnUserSubsystemRegistration => "User",
                _ => string.Empty,
            };

            if (subsystemCount <= 0)
            {
                this.Send("No ", type, " Subsystems found to register!").ToUnityConsole();
                return;
            }

            await Initium.PreloadBootAndInitAsync(wovenSubsystems);

            this.Send("All ", type, " Subsystems operational. Woven Subsystems: ", BuildWovenSubsystemsReport(wovenSubsystems)).ToUnityConsole();
        }
    }
}
