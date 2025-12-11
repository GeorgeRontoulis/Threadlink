namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using UnityEngine;

    /// <summary>
    /// Threadlink's environment-aware IO System.
    /// The purpose of this system is to allow for a singular entry point for read/write operations during the game's lifecycle.
    /// How data is serialized/deserialized is entirely customizable, however it should always be in bytes.
    /// Non-byte serialization schemes like JSON are not supported, since all environments typically work with bytes.
    /// <see cref="Sentinel"/> provides native behaviour for several environments:
    /// <list type="bullet">
    /// <item> <see cref="Steam"/> </item>
    /// <item> <see cref="XBOX"/> (Including Microsoft Store) </item>
    /// <item> <see cref="PlayStation"/> </item>
    /// <item> <see cref="NintendoSwitch"/> </item>
    /// </list>
    /// Where applicable, the native environments use <see cref="Application.persistentDataPath"/> 
    /// for their operations. Naturally, this behaviour may also be overridden as needed.
    /// </summary>
    public sealed partial class Sentinel : ThreadlinkSubsystem<Sentinel>, IThreadlinkDependency<SentinelConfig>, IAddressablesPreloader
    {
        public enum OperationState : byte { Idle, Deploying, Reading, Writing }

        public bool EnvironmentDeployed { get; private set; } = false;
        public OperationState CurrentOperationState { get; private set; } = OperationState.Idle;

        private Environment TargetEnvironment { get; set; }

        public async UniTask<bool> TryPreloadAssetsAsync() => TryConsumeDependency(await Threadlink.Instance.NativeConfig.LoadSentinelConfigAsync());

        public bool TryConsumeDependency(SentinelConfig input)
        {
            if (input == null)
                return false;

            TargetEnvironment = input.TargetEnvironment;
            return true;
        }

        public override void Boot()
        {
            base.Boot();

            EnvironmentDeployed = false;
            CurrentOperationState = OperationState.Idle;
        }

        #region Internal API:
        public async UniTask DeployEnvironmentAsync()
        {
            if (EnvironmentDeployed || CurrentOperationState is not OperationState.Idle) return;

            if (TargetEnvironment == null)
            {
                this.Send("The Target Environment has not been assigned!").ToUnityConsole(DebugType.Error);
                EnvironmentDeployed = false;
                return;
            }

            if (Application.isEditor)
            {
                EnvironmentDeployed = true;
                return;
            }

            CurrentOperationState = OperationState.Deploying;

            EnvironmentDeployed = await TargetEnvironment.TryDeployAsync();

            CurrentOperationState = OperationState.Idle;
        }

        public async UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData)
        {
            if (!EnvironmentDeployed || CurrentOperationState is not OperationState.Idle) return false;

            CurrentOperationState = OperationState.Writing;

            var result = await TargetEnvironment.TryWriteToStorageAsync(folderID, fileID, serializedData);

            CurrentOperationState = OperationState.Idle;

            return result;
        }

        public async UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID)
        {
            if (!EnvironmentDeployed || CurrentOperationState is not OperationState.Idle) return null;

            CurrentOperationState = OperationState.Reading;

            var result = await TargetEnvironment.ReadFromStorageAsync(folderID, fileID);

            CurrentOperationState = OperationState.Idle;

            return result;
        }

        public void DeleteStoredData(string folderID, string fileID)
        {
            if (EnvironmentDeployed)
                TargetEnvironment.DeleteStoredData(folderID, fileID);
        }
        #endregion
    }
}