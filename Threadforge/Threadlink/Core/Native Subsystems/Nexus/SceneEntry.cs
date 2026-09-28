namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Cysharp.Threading.Tasks;
    using Generated;
    using System.Runtime.CompilerServices;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        /// <summary>
        /// A scene Nexus can hold and present: its Addressables pointer, how it loads, its audio, and hooks around its
        /// load and unload. Scenes always load additively, beside the persistent scene the game starts in.
        /// </summary>
        public interface ISceneEntry
        {
            public ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
            public ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
            public ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
            public float MusicVolume { get; }
            public float AtmosVolume { get; }

            /// <summary>
            /// The physics scene the scene loads into. <see cref="LocalPhysicsMode.None"/> shares the default physics scene.
            /// Scenes resident alongside other scenes should use their own, for example <see cref="LocalPhysicsMode.Physics2D"/>.
            /// </summary>
            public LocalPhysicsMode PhysicsMode => LocalPhysicsMode.None;

            /// <summary>
            /// How long the scene stays resident once nothing holds it, in seconds, unless a release says otherwise: a
            /// hold within that time keeps it without reloading, as when stepping out of a house and back in.
            /// </summary>
            public float UnloadDelay => 0f;

            /// <summary>Once the scene has loaded and its objects booted, before anyone holding it gets it.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public async UniTask OnFinishedLoadingAsync() => await UniTask.CompletedTask;

            /// <summary>Before the scene unloads, while its objects are still there, undiscarded.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public async UniTask OnBeforeUnloadedAsync() => await UniTask.CompletedTask;
        }
    }
}
