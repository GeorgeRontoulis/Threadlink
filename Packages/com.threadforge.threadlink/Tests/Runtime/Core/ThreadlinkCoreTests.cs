namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class ThreadlinkCoreTests
    {
        private static readonly List<string> Log = new List<string>();

        public readonly struct Ping
        {
            public readonly string From;

            public Ping(string from) => From = from;
        }

        [ManualRegistration]
        public sealed class Clock : ThreadlinkSubsystem<Clock>
        {
            public int Ticks;

            protected override void OnSubsystemBoot() => Log.Add("boot Clock");

            protected override void OnSubsystemInit() => Log.Add("init Clock");

            protected override void OnSubsystemDeinit() => Log.Add("deinit Clock");

            protected override void OnSubsystemShutdown() => Log.Add("stop Clock");

            public static void Tick() => Instance.Ticks++;
        }

        [ManualRegistration]
        public sealed class Wallet : ThreadlinkSubsystem<Wallet>
        {
            public Clock Clock;

            protected override void OnSubsystemBoot() => Log.Add("boot Wallet");

            protected override void OnSubsystemInit()
            {
                Clock = Clock.Instance;
                Log.Add("init Wallet");
            }

            protected override void OnSubsystemDeinit() => Log.Add("deinit Wallet");

            protected override void OnSubsystemShutdown() => Log.Add("stop Wallet");
        }

        // Sorts before Clock by name, so it initializes first and still finds Clock booted.
        [ManualRegistration]
        public sealed class Alarm : ThreadlinkSubsystem<Alarm>
        {
            public int TicksAtInit = -1;

            protected override void OnSubsystemInit() => TicksAtInit = Clock.Instance.Ticks;
        }

        [ManualRegistration]
        public sealed class Listener : ThreadlinkSubsystem<Listener>
        {
            public readonly List<string> Heard = new List<string>();
            private SubscriptionToken token;

            protected override void OnSubsystemBoot() => token = Iris.Subscribe<Ping>(ping => Heard.Add(ping.From));

            protected override void OnSubsystemShutdown() => token.Dispose();
        }

        // Sorts before Listener: its announcement during initialization still reaches a subscriber that booted later.
        [ManualRegistration]
        public sealed class Herald : ThreadlinkSubsystem<Herald>
        {
            protected override void OnSubsystemInit() => Iris.Notify(new Ping("Herald"));
        }

        [ManualRegistration]
        public sealed class Eager : ThreadlinkSubsystem<Eager>
        {
            protected override void OnSubsystemBoot() => _ = Clock.Instance;
        }

        [ManualRegistration]
        public sealed class Broken : ThreadlinkSubsystem<Broken>
        {
            protected override void OnSubsystemInit() => throw new InvalidOperationException("broken on purpose");
        }

        [ManualRegistration]
        public sealed class NeedsBroken : ThreadlinkSubsystem<NeedsBroken>
        {
            protected override void OnSubsystemInit() => _ = Broken.Instance;
        }

        [ManualRegistration]
        public sealed class MutualA : ThreadlinkSubsystem<MutualA>
        {
            public MutualB Other;

            protected override void OnSubsystemInit() => Other = MutualB.Instance;
        }

        [ManualRegistration]
        public sealed class MutualB : ThreadlinkSubsystem<MutualB>
        {
            public MutualA Other;

            protected override void OnSubsystemInit() => Other = MutualA.Instance;
        }

        [SetUp]
        public void SetUp() => Log.Clear();

        [Test]
        public void EverySubsystemBootsBeforeAnyInitializesAndShutdownMirrorsIt()
        {
            using (var runtime = Threadlink.DeployIsolated(typeof(Wallet), typeof(Clock)))
            {
                var wallet = Wallet.Instance;
                Assert.That(wallet.State, Is.EqualTo(SubsystemState.Ready));
                Assert.That(wallet.Clock, Is.SameAs(Clock.Instance));
                Assert.That(wallet.Dependencies, Is.EquivalentTo(new[] { typeof(Clock) }), "What a subsystem uses while it initializes is recorded for the monitor.");
                Clock.Tick();
                Assert.That(Clock.Instance.Ticks, Is.EqualTo(1));
            }
            // Clock sorts before Wallet by name.
            Assert.That(
                Log,
                Is.EqualTo(new[] { "boot Clock", "boot Wallet", "init Clock", "init Wallet", "deinit Wallet", "deinit Clock", "stop Wallet", "stop Clock" })
            );
        }

        [Test]
        public void InitializationCanUseAnySubsystemWhateverItsOrder()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Clock), typeof(Alarm));
            Assert.That(Alarm.Instance.State, Is.EqualTo(SubsystemState.Ready));
            Assert.That(Alarm.Instance.TicksAtInit, Is.EqualTo(0), "Alarm initialized before Clock, and Clock had already booted.");
        }

        [Test]
        public void IrisBootsFirstSoBootTimeSubscribersHearEveryInitializationEvent()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Listener), typeof(Herald), typeof(Iris));
            Assert.That(runtime.Subsystems[0], Is.TypeOf<Iris>());
            Assert.That(Listener.Instance.Heard, Is.EqualTo(new[] { "Herald" }));
        }

        [Test]
        public void UsingAnotherSubsystemWhileBootingFails()
        {
            LogAssert.Expect(LogType.Error, new Regex("Eager failed to boot. Eager used Clock in OnSubsystemBoot"));
            using var runtime = Threadlink.DeployIsolated(typeof(Eager), typeof(Clock));
            Assert.That(Eager.TryGetSingleton(out _), Is.False);
            Assert.That(Clock.Instance.State, Is.EqualTo(SubsystemState.Ready), "The other subsystems still start.");
        }

        [Test]
        public void SubsystemsMayUseEachOtherDuringInitialization()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(MutualA), typeof(MutualB));
            Assert.That(MutualA.Instance.Other, Is.SameAs(MutualB.Instance));
            Assert.That(MutualB.Instance.Other, Is.SameAs(MutualA.Instance));
        }

        [Test]
        public void SubsystemsUsedOnlyInATestSessionJoinOnFirstUse()
        {
            using var runtime = Threadlink.DeployIsolated();
            var wallet = runtime.Add<Wallet>();
            Assert.That(wallet.State, Is.EqualTo(SubsystemState.Ready));
            Assert.That(Clock.TryGetSingleton(out var clock), Is.True);
            Assert.That(wallet.Clock, Is.SameAs(clock));
        }

        [Test]
        public void TryGetSingletonRequiresLinkageToTheCurrentCore()
        {
            Assert.That(Wallet.TryGetSingleton(out _), Is.False);
            Wallet kept;
            using (var runtime = Threadlink.DeployIsolated())
            {
                kept = runtime.Add<Wallet>();
                Assert.That(Wallet.TryGetSingleton(out var linked), Is.True);
                Assert.That(linked, Is.SameAs(kept));
                Assert.That(Threadlink.TryGetSingleton(out var core), Is.True);
                Assert.That(core, Is.SameAs(runtime));
            }
            Assert.That(kept.State, Is.EqualTo(SubsystemState.ShutDown));
            Assert.That(Wallet.TryGetSingleton(out _), Is.False);
            Assert.Throws<SubsystemUnavailableException>(() => _ = Wallet.Instance);
        }

        [Test]
        public void TryGetSingletonNeverStartsASubsystem()
        {
            using var runtime = Threadlink.DeployIsolated();
            Assert.That(Clock.TryGetSingleton(out _), Is.False);
            Assert.That(runtime.Subsystems, Is.Empty);
        }

        [Test]
        public void NestedIsolatedCoresRestoreThePreviousLinks()
        {
            using var outer = Threadlink.DeployIsolated();
            var first = outer.Add<Clock>();
            using (var inner = Threadlink.DeployIsolated())
            {
                Assert.That(Clock.TryGetSingleton(out _), Is.False, "The outer subsystem belongs to another core.");
                var second = inner.Add<Clock>();
                Assert.That(Clock.Instance, Is.SameAs(second));
            }
            Assert.That(Threadlink.Current, Is.SameAs(outer));
            Assert.That(Clock.Instance, Is.SameAs(first));
        }

        [Test]
        public void FailuresStayWithTheFailingSubsystemAndThoseThatUseIt()
        {
            LogAssert.Expect(LogType.Exception, new Regex("broken on purpose"));
            LogAssert.Expect(LogType.Error, new Regex("NeedsBroken failed to initialize. NeedsBroken uses Broken, which failed to start: broken on purpose"));
            using var runtime = Threadlink.DeployIsolated(typeof(NeedsBroken), typeof(Broken), typeof(Clock));
            Assert.That(NeedsBroken.TryGetSingleton(out _), Is.False);
            Assert.That(Clock.Instance.State, Is.EqualTo(SubsystemState.Ready));
            var error = Assert.Throws<SubsystemUnavailableException>(() => _ = Broken.Instance);
            StringAssert.Contains("broken on purpose", error.Message);
        }

        [Test]
        public void FailuresAndTheSessionEndAreAnnouncedOnIris()
        {
            var failed = new List<string>();
            int ending = 0;
            LogAssert.Expect(LogType.Exception, new Regex("broken on purpose"));
            var runtime = Threadlink.DeployIsolated();
            runtime.Add<Iris>();
            Iris.Subscribe<SubsystemFailed>(failure => failed.Add(failure.Name));
            Iris.Subscribe<SessionEnding>(ended => ending += ended.Session == runtime.SessionId ? 1 : 100);
            runtime.Add<Broken>();
            Assert.That(failed, Is.EqualTo(new[] { "Broken" }));
            runtime.Dispose();
            Assert.That(ending, Is.EqualTo(1));
        }

        [Test]
        public void ThreadlinkReadyReachesSubscribersThatBooted()
        {
            ReadyWatcher.Seen = null;
            using var runtime = Threadlink.DeployIsolated(typeof(ReadyWatcher), typeof(Clock));
            Assert.That(ReadyWatcher.Seen.HasValue, Is.True);
            Assert.That(ReadyWatcher.Seen.Value.Session, Is.EqualTo(runtime.SessionId));
            Assert.That(ReadyWatcher.Seen.Value.Ready, Is.EqualTo(3), "Iris, the watcher and Clock.");
            Assert.That(ReadyWatcher.Seen.Value.Failed, Is.Zero);
        }

        [ManualRegistration]
        public sealed class ReadyWatcher : ThreadlinkSubsystem<ReadyWatcher>
        {
            public static ThreadlinkReady? Seen;
            private SubscriptionToken token;

            protected override void OnSubsystemBoot() => token = Iris.Subscribe<ThreadlinkReady>(ready => Seen = ready);

            protected override void OnSubsystemShutdown() => token.Dispose();
        }

        [Test]
        public void LifetimeIsCancelledWhenTheSessionEnds()
        {
            var runtime = Threadlink.DeployIsolated();
            var lifetime = Threadlink.Lifetime;
            Assert.That(lifetime.IsCancellationRequested, Is.False);
            runtime.Dispose();
            Assert.That(lifetime.IsCancellationRequested, Is.True);
            Assert.Throws<ObjectDisposedException>(() => runtime.Add<Clock>());
        }

        [Test]
        public void MonitorReportsListEverySubsystem()
        {
            using var runtime = Threadlink.DeployIsolated();
            runtime.Add<Wallet>();
            var report = new MonitorReport();
            report.Begin(true);
            runtime.Report(report);
            Assert.That(report.Caption, Is.EqualTo("2 ready"));
            Assert.That(report.Lines, Has.Some.Matches<MonitorLine>(line => line.Label.EndsWith("Wallet")));
        }
    }
}
