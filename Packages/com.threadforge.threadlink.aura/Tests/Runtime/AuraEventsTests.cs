namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System.Collections;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class AuraEventsTests
    {
        private static readonly AudioCategory Category = new AudioCategory("Events Test");

        [Test]
        public void VolumeAndMuteChangesAreAnnounced()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Aura));
            var heard = new List<string>();
            Iris.Subscribe<VolumeChanged>(e => heard.Add("volume " + e.Category.Name + " " + (int)(e.Volume * 100)));
            Iris.Subscribe<MuteChanged>(e => heard.Add("mute " + e.Category.Name + " " + e.Muted));
            Aura.SetVolume(Category, 0.5f);
            Aura.SetMute(Category, true);
            Aura.FadeVolumeAsync(Category, 0.25f, 0).GetAwaiter().GetResult();
            Assert.That(heard, Is.EqualTo(new[] { "volume Events Test 50", "mute Events Test True", "volume Events Test 25" }));
        }

        [UnityTest]
        public IEnumerator MusicChangesAreAnnounced()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            var states = new bool[listeners.Length];
            for (int i = 0; i < listeners.Length; i++)
                states[i] = listeners[i].enabled;
            var root = new GameObject("Music events test");
            var clip = AudioClip.Create("Silent music", 44100, 1, 44100, false);
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.Volume = Vector2.zero;
            cue.Variations = new[] { new AudioCue.Variation { Clip = clip } };
            var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Aura));
            try
            {
                Aura.UseListener(root.AddComponent<AudioListener>());
                var changes = new List<MusicChanged>();
                Iris.Subscribe<MusicChanged>(changes.Add);
                Aura.PlayMusic(cue, 0);
                yield return null;
                Aura.PlayMusic(cue, 0);
                Aura.StopMusic(0);
                Aura.StopMusic(0);
                Assert.That(changes, Has.Count.EqualTo(2), "Playing the music already playing, or stopping silence, announces nothing.");
                Assert.That(changes[0].Previous, Is.Null);
                Assert.That(changes[0].Current, Is.SameAs(cue));
                Assert.That(changes[1].Previous, Is.SameAs(cue));
                Assert.That(changes[1].Current, Is.Null);
            }
            finally
            {
                runtime.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cue);
                Object.DestroyImmediate(clip);
                for (int i = 0; i < listeners.Length; i++)
                    if (listeners[i] != null)
                        listeners[i].enabled = states[i];
            }
        }
    }
}
