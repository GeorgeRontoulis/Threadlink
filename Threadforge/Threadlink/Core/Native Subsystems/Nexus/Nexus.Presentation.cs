namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Core;
    using Cysharp.Threading.Tasks;
    using Generated;
    using Scribe;
    using Shared;
    using Unity.Scripting.LifecycleManagement;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        /// <summary>The scene the local player sees and hears: the last one <see cref="PresentAsync"/> presented, or null.</summary>
        public static ISceneEntry Presented { get; private set; }

        /// <summary>The hold presentation keeps on <see cref="Presented"/>'s scene.</summary>
        [NoAutoStaticsCleanup]
        private static SceneHold presentedHold;

        /// <summary>The presentation running now, which the next one waits for: presentations run one at a time.</summary>
        [NoAutoStaticsCleanup]
        private static UniTaskCompletionSource presentationTurn;

        /// <summary>
        /// Present <paramref name="entry"/>'s scene to the local player: hold it (loading it if needed), make it the active
        /// scene, transition to its audio, publish <see cref="ThreadlinkIDs.Iris.Events.OnScenePresented"/>, then release the
        /// hold on the scene presented before, which unloads once nothing else holds it. Presentations run one at a time,
        /// in call order. Wrap it in the fader and loading screen with <see cref="TransitionAsync"/>.
        /// </summary>
        /// <returns>False if the scene could not be loaded; what was presented stays presented.</returns>
        public static async UniTask<bool> PresentAsync(ISceneEntry entry)
        {
            var turn = await TakeTurnAsync();

            try
            {
                return await PresentNowAsync(entry);
            }
            finally
            {
                EndTurn(turn);
            }
        }

        /// <summary>
        /// Present nothing: let go of the presented scene, which unloads once nothing else holds it, for the time between
        /// one set of scenes and the next (a session's end, before its menu is presented). Runs in turn with presentations.
        /// </summary>
        public static async UniTask StopPresentingAsync()
        {
            var turn = await TakeTurnAsync();

            try
            {
                var hold = presentedHold;

                presentedHold = null;
                Presented = null;
                hold?.Release();
            }
            finally
            {
                EndTurn(turn);
            }
        }

        /// <summary>Wait for the presentation before this one, if any: presentations run one at a time, in call order.</summary>
        private static async UniTask<UniTaskCompletionSource> TakeTurnAsync()
        {
            var previousTurn = presentationTurn;
            var turn = new UniTaskCompletionSource();

            presentationTurn = turn;

            if (previousTurn != null)
                await previousTurn.Task;

            return turn;
        }

        private static void EndTurn(UniTaskCompletionSource turn)
        {
            if (ReferenceEquals(presentationTurn, turn))
                presentationTurn = null;

            turn.TrySetResult();
        }

        /// <summary>
        /// Move the local player to <paramref name="entry"/>'s scene behind the fader and loading screen: the single-scene
        /// workflow in one call. The scene presented before unloads once nothing else holds it.
        /// <code>
        /// await Nexus.TransitionAsync(townEntry);
        /// </code>
        /// </summary>
        /// <returns>False if the scene could not be loaded; what was presented stays presented.</returns>
        public static async UniTask<bool> TransitionAsync(ISceneEntry entry)
        {
            await FadeToLoadingScreenAsync();

            bool presented = await PresentAsync(entry);

            await FadeToGameplayAsync();
            return presented;
        }

        /// <summary>The scene of <see cref="Presented"/>.</summary>
        public static bool TryGetPresentedScene(out Scene scene)
        {
            scene = default;
            return Presented != null && TryGetResidentScene(Presented.ScenePointer, out scene);
        }

        private static async UniTask<bool> PresentNowAsync(ISceneEntry entry)
        {
            if (entry == null)
                return false;

            if (Presented != null && Presented.ScenePointer == entry.ScenePointer && presentedHold != null && presentedHold.IsHeld)
                return true;

            var hold = await HoldAsync(entry);

            if (hold == null)
            {
                Scribe.Send<Threadlink>("Cannot present ", entry.ScenePointer.ToString(), ": it could not be loaded.").ToUnityConsole(DebugType.Error);
                return false;
            }

            SceneManager.SetActiveScene(hold.Scene);

            var previous = presentedHold;

            presentedHold = hold;
            Presented = entry;

            await TransitionAudioAsync(entry);
            Publish(ThreadlinkIDs.Iris.Events.OnScenePresented, entry);

            previous?.Release();
            return true;
        }

        /// <summary>Nothing is presented any more; with <paramref name="release"/>, the presented scene's hold is let go.</summary>
        private static void ClearPresentation(bool release)
        {
            var hold = presentedHold;

            presentedHold = null;
            presentationTurn = null;
            Presented = null;

            if (release)
                hold?.Release(0f);
        }
    }
}
