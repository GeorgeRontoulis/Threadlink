namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class ThreadlinkCoreTests
    {
        private static readonly List<string> Log = new List<string>();

        [ManualRegistration]
        public sealed class Clock : ThreadlinkSubsystem<Clock>
        {
            public int Ticks;

            protected override void OnInitialize() => Log.Add("init Clock");

            protected override void OnShutdown() => Log.Add("stop Clock");

            public static void Tick() => Instance.Ticks++;
        }

        [ManualRegistration]
        public sealed class Wallet : ThreadlinkSubsystem<Wallet>
        {
            public Clock Clock;

            protected override void OnInitialize()
            {
                Clock = Require<Clock>();
                Log.Add("init Wallet");
            }

            protected override void OnShutdown() => Log.Add("stop Wallet");
        }

        [ManualRegistration]
        public sealed class Broken : ThreadlinkSubsystem<Broken>
        {
            protected override void OnInitialize() => throw new InvalidOperationException("broken on purpose");
        }

        [ManualRegistration]
        public sealed class NeedsBroken : ThreadlinkSubsystem<NeedsBroken>
        {
            protected override void OnInitialize() => Require<Broken>();
        }

        [ManualRegistration]
        public sealed class CycleA : ThreadlinkSubsystem<CycleA>
        {
            protected override void OnInitialize() => Require<CycleB>();
        }

        [ManualRegistration]
        public sealed class CycleB : ThreadlinkSubsystem<CycleB>
        {
            protected override void OnInitialize() => Require<CycleA>();
        }

        [SetUp]
        public void SetUp() => Log.Clear();

        [Test]
        public void RequiredSubsystemsInitializeFirstAndShutDownLast()
        {
            using (var runtime = Threadlink.DeployIsolated())
            {
                var wallet = runtime.Add<Wallet>();
                Assert.That(wallet.State, Is.EqualTo(SubsystemState.Ready));
                Assert.That(wallet.Clock, Is.SameAs(Clock.Instance));
                Assert.That(wallet.Dependencies, Is.EquivalentTo(new[] { typeof(Clock) }));
                Clock.Tick();
                Assert.That(Clock.Instance.Ticks, Is.EqualTo(1));
            }
            Assert.That(Log, Is.EqualTo(new[] { "init Clock", "init Wallet", "stop Wallet", "stop Clock" }));
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
        public void FailuresStayWithTheFailingSubsystemAndItsDependents()
        {
            using var runtime = Threadlink.DeployIsolated();
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("broken on purpose"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("NeedsBroken failed to initialize"));
            var dependent = runtime.Add<NeedsBroken>();
            var clock = runtime.Add<Clock>();
            Assert.That(dependent.State, Is.EqualTo(SubsystemState.Failed));
            Assert.That(dependent.Error, Is.TypeOf<SubsystemUnavailableException>());
            Assert.That(clock.State, Is.EqualTo(SubsystemState.Ready));
            var error = Assert.Throws<SubsystemUnavailableException>(() => _ = Broken.Instance);
            StringAssert.Contains("broken on purpose", error.Message);
        }

        [Test]
        public void CyclesAreRejectedWithTheChain()
        {
            using var runtime = Threadlink.DeployIsolated();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CycleB failed to initialize.*cycle: CycleA → CycleB → CycleA"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CycleA failed to initialize"));
            var a = runtime.Add<CycleA>();
            Assert.That(a.State, Is.EqualTo(SubsystemState.Failed));
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
