namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Cysharp.Threading.Tasks;
    using Generated;
    using System.Runtime.CompilerServices;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        public interface ISceneEntry
        {
            public ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
            public LoadSceneMode LoadMode { get; }
            public ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
            public ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
            public float MusicVolume { get; }
            public float AtmosVolume { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public async UniTask OnFinishedLoadingAsync() => await UniTask.CompletedTask;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public async UniTask OnBeforeUnloadedAsync() => await UniTask.CompletedTask;
        }
    }
}