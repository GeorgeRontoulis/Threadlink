namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class ChronosTests
    {
        private float scale;

        [SetUp]
        public void Remember() => scale = Time.timeScale;

        [TearDown]
        public void Restore() => Time.timeScale = scale;

        [Test]
        public void PauseClaimsComposeAndAnnounceOnlyTheirEdges()
        {
            using var core = Threadlink.DeployIsolated(typeof(Iris), typeof(Chronos));
            var changes = new List<bool>();
            using var heard = Iris.Subscribe<PauseChanged>(change => changes.Add(change.Paused));
            Chronos.TimeScale = 0.5f;

            var menu = Chronos.Pause();
            var dialog = Chronos.Pause();
            Assert.That(Chronos.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            menu.Dispose();
            menu.Dispose();
            Assert.That(Chronos.PauseClaims, Is.EqualTo(1), "disposing a claim twice releases it once");
            Assert.That(Time.timeScale, Is.Zero);
            dialog.Dispose();

            Assert.That(Chronos.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TimeScaleSetWhilePausedAppliesOnResume()
        {
            using var core = Threadlink.DeployIsolated(typeof(Iris), typeof(Chronos));
            using (Chronos.Pause())
            {
                Chronos.TimeScale = 2;
                Assert.That(Time.timeScale, Is.Zero);
            }
            Assert.That(Time.timeScale, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Chronos.TimeScale = -1);
        }

        [Test]
        public void SessionEndRestoresTheTimeScaleAndIgnoresLateClaims()
        {
            float before = Time.timeScale;
            IDisposable late;
            using (Threadlink.DeployIsolated(typeof(Iris), typeof(Chronos)))
            {
                Chronos.TimeScale = 3;
                late = Chronos.Pause();
            }
            Assert.That(Time.timeScale, Is.EqualTo(before));
            Assert.DoesNotThrow(late.Dispose);
        }

        [Test]
        public void PlaytimeCanBeRestoredButNotNegative()
        {
            using var core = Threadlink.DeployIsolated(typeof(Iris), typeof(Chronos));
            Chronos.SetPlaytime(TimeSpan.FromMinutes(90));
            Assert.That(Chronos.Playtime, Is.GreaterThanOrEqualTo(TimeSpan.FromMinutes(90)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Chronos.SetPlaytime(TimeSpan.FromSeconds(-1)));
        }

        [UnityTest]
        public IEnumerator PlaytimeCountsFramesUnlessPausedOrTurnedOff()
        {
            using var core = Threadlink.DeployIsolated(typeof(Iris), typeof(Chronos));
            Chronos.SetPlaytime(TimeSpan.Zero);
            for (int i = 0; i < 3; i++)
                yield return null;
            var played = Chronos.Playtime;
            Assert.That(played, Is.GreaterThan(TimeSpan.Zero));

            var pause = Chronos.Pause();
            for (int i = 0; i < 3; i++)
                yield return null;
            Assert.That(Chronos.Playtime, Is.EqualTo(played), "a paused game does not count");
            pause.Dispose();

            Chronos.CountsPlaytime = false;
            played = Chronos.Playtime;
            for (int i = 0; i < 3; i++)
                yield return null;
            Assert.That(Chronos.Playtime, Is.EqualTo(played));
        }

        [Test]
        public void ExternalDriversPublishFramesThemselves()
        {
            using var core = Threadlink.DeployIsolated(typeof(Iris));
            float received = 0;
            int fixedFrames = 0,
                lateFrames = 0;
            using var update = Iris.Subscribe<UpdateFrame>(frame => received += frame.DeltaTime);
            using var fixedStep = Iris.Subscribe<FixedFrame>(_ => fixedFrames++);
            using var late = Iris.Subscribe<LateFrame>(_ => lateFrames++);

            Assert.Throws<InvalidOperationException>(() => Iris.RunUpdate(1));
            using (Iris.DriveFramesExternally())
            {
                Assert.That(Iris.FramesAreExternal, Is.True);
                Iris.RunUpdate(0.25f);
                Iris.RunFixedUpdate(0.02f);
                Iris.RunLateUpdate(0.25f);
            }
            Assert.That(Iris.FramesAreExternal, Is.False);
            Assert.That(received, Is.EqualTo(0.25f));
            Assert.That((fixedFrames, lateFrames), Is.EqualTo((1, 1)));
        }

        [Test]
        public void AnimatorHashesHashTheirNameAfterDeserialization()
        {
            var hash = new AnimatorHash("Speed");
            Assert.That((int)hash, Is.EqualTo(Animator.StringToHash("Speed")));
            var copy = JsonUtility.FromJson<Holder>("{\"Hash\":{\"name\":\"Jump\"}}");
            Assert.That(copy.Hash.Value, Is.EqualTo(Animator.StringToHash("Jump")));
            Assert.That(copy.Hash.IsAssigned, Is.True);
            Assert.That(default(AnimatorHash).IsAssigned, Is.False);
            Assert.Throws<ArgumentException>(() => new MinMaxRangeAttribute(2, 1));
        }

        [Serializable]
        private sealed class Holder
        {
            public AnimatorHash Hash;
        }
    }
}
