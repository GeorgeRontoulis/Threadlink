namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;

    /// <summary>Joins Nexus transitions: warms the next scene's music while it loads and starts it on presentation.</summary>
    internal sealed class SceneMusicDirector : ThreadlinkSubsystem<SceneMusicDirector>
    {
        private IDisposable step;
        private int started;

        public override string Name => "Scene Music";

        protected override void OnInitialize()
        {
            Require<Aura>();
            Require<Nexus>();
            step = Nexus.AddTransitionStep(PrepareAsync, Committed);
        }

        protected override void OnShutdown() => step?.Dispose();

        public override void Report(MonitorReport report)
        {
            report.Summary(started + " started");
            if (report.Detailed)
                report.Section("Scene music").Field("Started on presentation", started);
        }

#pragma warning disable CS1998 // Warming is synchronous; the step contract is awaitable.
        private static async UniTask PrepareAsync(SceneTransition transition, CancellationToken token)
        {
            var music = Find(transition);
            if (music != null && music.Music != null)
                music.Music.WarmAudioData();
        }
#pragma warning restore CS1998

        private void Committed(SceneTransition transition)
        {
            var music = Find(transition);
            if (music == null)
                return;
            Aura.PlayMusic(music.Music, music.CrossfadeSeconds);
            started++;
        }

        private static SceneMusic Find(SceneTransition transition)
        {
            if (!transition.To.IsValid() || !transition.To.isLoaded)
                return null;
            foreach (var root in transition.To.GetRootGameObjects())
            {
                var music = root.GetComponentInChildren<SceneMusic>(true);
                if (music != null)
                    return music;
            }
            return null;
        }
    }
}
