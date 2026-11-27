// Subsystems written against Threadlink 1.0 keep working: OnInitialize, OnShutdown and Require are obsolete, not removed.
#pragma warning disable CS0618, CS0672
namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class LegacyLifecycleTests
    {
        private static readonly List<string> Log = new List<string>();

        [ManualRegistration]
        public sealed class OldClock : ThreadlinkSubsystem<OldClock>
        {
            public int Ticks = -1;

            protected override void OnInitialize()
            {
                Ticks = 0;
                Log.Add("init OldClock");
            }

            protected override void OnShutdown() => Log.Add("stop OldClock");
        }

        [ManualRegistration]
        public sealed class OldWallet : ThreadlinkSubsystem<OldWallet>
        {
            public OldClock Clock;

            protected override void OnInitialize()
            {
                Clock = Require<OldClock>();
                Log.Add("init OldWallet");
            }

            protected override void OnShutdown() => Log.Add("stop OldWallet");
        }

        // Sorts before OldClock and reads state OldClock sets in its OnInitialize, as 1.0 allowed through Instance.
        [ManualRegistration]
        public sealed class NewReader : ThreadlinkSubsystem<NewReader>
        {
            public int TicksAtInit = -2;

            protected override void OnSubsystemInit() => TicksAtInit = OldClock.Instance.Ticks;
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
                var wallet = runtime.Add<OldWallet>();
                Assert.That(wallet.State, Is.EqualTo(SubsystemState.Ready));
                Assert.That(wallet.Clock, Is.SameAs(OldClock.Instance));
                Assert.That(wallet.Dependencies, Is.EquivalentTo(new[] { typeof(OldClock) }));
            }
            Assert.That(Log, Is.EqualTo(new[] { "init OldClock", "init OldWallet", "stop OldWallet", "stop OldClock" }));
        }

        [Test]
        public void A1Point0SubsystemIsInitializedWhenANewOneUsesIt()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(OldClock), typeof(NewReader));
            Assert.That(NewReader.Instance.TicksAtInit, Is.EqualTo(0), "OldClock's OnInitialize ran before NewReader read it.");
        }

        [Test]
        public void RequireStillRejectsCyclesWithTheChain()
        {
            using var runtime = Threadlink.DeployIsolated();
            LogAssert.Expect(LogType.Error, new Regex("CycleB failed to initialize.*cycle: CycleA → CycleB → CycleA"));
            LogAssert.Expect(LogType.Error, new Regex("CycleA failed to initialize"));
            var a = runtime.Add<CycleA>();
            Assert.That(a.State, Is.EqualTo(SubsystemState.Failed));
        }
    }
}
#pragma warning restore CS0618, CS0672
