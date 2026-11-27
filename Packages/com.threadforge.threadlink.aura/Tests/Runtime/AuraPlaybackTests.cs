namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class AuraPlaybackTests
    {
        private GameObject root;
        private AuraEngine engine;
        private AudioCue cue;
        private AudioClip clip;
        private AudioListener[] previous;
        private bool[] listenerStates;
        private readonly System.Collections.Generic.Dictionary<AudioListener, bool> lateListeners =
            new System.Collections.Generic.Dictionary<AudioListener, bool>();

        private void SelectTestListener()
        {
            var selected = root.GetComponent<AudioListener>();
            foreach (
                var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include
                )
            )
                if (
                    listener != selected
                    && Array.IndexOf(previous, listener) < 0
                    && !lateListeners.ContainsKey(listener)
                )
                    lateListeners.Add(listener, listener.enabled);
            engine.SelectListener(selected);
        }

        private sealed class Retention : IDisposable
        {
            internal int Released;

            public void Dispose()
            {
                Released++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            previous = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include
            );
            listenerStates = new bool[previous.Length];
            for (int i = 0; i < previous.Length; i++)
            {
                listenerStates[i] = previous[i].enabled;
                previous[i].enabled = false;
            }
            root = new GameObject("Audio test");
            root.AddComponent<AudioListener>();
            engine = root.AddComponent<AuraEngine>();
            clip = AudioClip.Create("Silent test", 44100, 1, 44100, false);
            cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.Volume = Vector2.zero;
            cue.Variations = new[] { new AudioCue.Variation { Clip = clip } };
            cue.VoiceLimit = 1;
            cue.Loop = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cue);
            UnityEngine.Object.DestroyImmediate(clip);
            for (int i = 0; i < previous.Length; i++)
                if (previous[i] != null)
                    previous[i].enabled = listenerStates[i];
            foreach (var entry in lateListeners)
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            lateListeners.Clear();
        }

        [Test]
        public void ZonesLowerTheirCategoriesInProportionToTheListenersDistance()
        {
            SelectTestListener();
            var place = new GameObject("Waterfall");
            try
            {
                place.transform.position = root.transform.position + Vector3.forward * 5;
                var zone = place.AddComponent<AudioZone>();
                zone.Radius = 10;
                zone.Lowers.Clear();
                zone.Lowers.Add(new AudioZone.Lowering { Category = "Music", Amount = 0.8f });
                var second = place.AddComponent<AudioZone>();
                second.Radius = 20;
                second.Lowers.Clear();
                second.Lowers.Add(new AudioZone.Lowering { Category = "Music", Amount = 0.2f });

                engine.DuckForZones();
                Assert.That(engine.DuckOf("Music"), Is.EqualTo(0.4f).Within(1e-4f), "halfway to the edge: half the amount; the strongest zone wins");
                Assert.That(engine.DuckOf("Effects"), Is.Zero);

                place.transform.position = root.transform.position + Vector3.forward * 50;
                engine.DuckForZones();
                Assert.That(engine.DuckOf("Music"), Is.Zero, "beyond the radius a zone does nothing");

                place.transform.position = root.transform.position;
                zone.enabled = false;
                engine.DuckForZones();
                Assert.That(engine.DuckOf("Music"), Is.EqualTo(0.2f).Within(1e-4f), "a disabled zone stops acting");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(place);
            }
            engine.DuckForZones();
            Assert.That(engine.DuckOf("Music"), Is.Zero);
        }

        [Test]
        public void VoiceLimitsAndStealingReleaseOnlyAdmittedRetention()
        {
            SelectTestListener();
            var retention = new Retention();
            var first = engine.Play(cue, retention: retention);
            Assert.That(engine.Play(cue), Is.Null);
            Assert.That(retention.Released, Is.Zero);
            cue.Overflow = VoiceOverflow.StealLowestPriority;
            var replacement = engine.Play(cue);
            Assert.That(first.Completion.GetAwaiter().GetResult(), Is.EqualTo(PlaybackEnd.Stolen));
            Assert.That(retention.Released, Is.EqualTo(1));
            replacement.Stop();
            replacement.Stop();
            Assert.That(engine.ActiveVoices, Is.Zero);
            Assert.That(engine.PooledSources, Is.GreaterThan(0));
            Assert.That(engine.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator OverlappingFadesCancelSupersededWorkAndCompleteStop()
        {
            SelectTestListener();
            var voice = engine.Play(cue);
            var superseded = voice.FadeTo(0, 5);
            var fade = voice.FadeTo(0, 0, true);
            for (int i = 0; i < 120 && !fade.Status.IsCompleted(); i++)
                yield return null;
            Assert.That(superseded.Status.IsCanceled(), Is.True);
            Assert.That(fade.Status.IsCompletedSuccessfully(), Is.True);
            Assert.That(voice.Completion.GetAwaiter().GetResult(), Is.EqualTo(PlaybackEnd.Stopped));
            Assert.That(engine.ActiveVoices, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SoundsMayOutliveEmittersAndEngineDestructionReleasesContent()
        {
            SelectTestListener();
            var emitter = new GameObject("Emitter");
            var retention = new Retention();
            var voice = engine.Play(cue, emitter: emitter.transform, retention: retention);
            UnityEngine.Object.Destroy(emitter);
            yield return null;
            yield return null;
            Assert.That(voice.IsPlaying, Is.True);
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(voice.Completion.GetAwaiter().GetResult(), Is.EqualTo(PlaybackEnd.EngineDestroyed));
            Assert.That(retention.Released, Is.EqualTo(1));
        }

        [Test]
        public void MissingListenerAndInvalidCueAreExplicitFailures()
        {
            SelectTestListener();
            root.GetComponent<AudioListener>().enabled = false;
            Assert.Throws<InvalidOperationException>(() => engine.Play(cue));
            root.GetComponent<AudioListener>().enabled = true;
            cue.Variations[0].Weight = float.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => engine.Play(cue));
        }

        [UnityTest]
        public IEnumerator InitialGainStartsSilentAndFadesIn()
        {
            SelectTestListener();
            cue.Volume = Vector2.one;
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Play(cue, initialGain: 1.5f));
            var voice = engine.Play(cue, initialGain: 0);
            Assert.That(voice, Is.Not.Null);
            var source = root.GetComponentInChildren<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.volume, Is.Zero);
            var fade = voice.FadeTo(1, 0.05f);
            float started = Time.realtimeSinceStartup;
            while (!fade.Status.IsCompleted() && Time.realtimeSinceStartup - started < 5)
                yield return null;
            Assert.That(fade.Status.IsCompleted(), Is.True);
            Assert.That(source.volume, Is.EqualTo(1).Within(0.001f));
            voice.Stop();
        }
    }
}
