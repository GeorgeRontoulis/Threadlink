namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using NUnit.Framework;
    using UnityEngine.LowLevel;

    public sealed class UnityFramesTests
    {
        private sealed class ForeignMarker { }

        private PlayerLoopSystem original;
        private Threadlink runtime;

        [SetUp]
        public void SetUp()
        {
            original = PlayerLoop.GetCurrentPlayerLoop();
            runtime = Threadlink.DeployIsolated();
            runtime.Add<Iris>();
        }

        [TearDown]
        public void TearDown()
        {
            IrisFrames.Uninstall();
            runtime.Dispose();
            PlayerLoop.SetPlayerLoop(original);
        }

        [Test]
        public void InstallationIsIdempotentAndRemovalPreservesForeignMarkers()
        {
            var loop = original;
            var children = new PlayerLoopSystem[loop.subSystemList.Length + 1];
            Array.Copy(loop.subSystemList, children, loop.subSystemList.Length);
            children[children.Length - 1] = new PlayerLoopSystem { type = typeof(ForeignMarker) };
            loop.subSystemList = children;
            PlayerLoop.SetPlayerLoop(loop);
            IrisFrames.Install();
            int once = Count(PlayerLoop.GetCurrentPlayerLoop());
            IrisFrames.Install();
            Assert.That(Count(PlayerLoop.GetCurrentPlayerLoop()), Is.EqualTo(once));
            IrisFrames.Uninstall();
            Assert.That(Count(PlayerLoop.GetCurrentPlayerLoop()), Is.EqualTo(Count(original) + 1));
            Assert.That(PlayerLoop.GetCurrentPlayerLoop().subSystemList[children.Length - 1].type, Is.EqualTo(typeof(ForeignMarker)));
        }

        [Test]
        public void MissingAnchorsDoNotPublishPartialLoop()
        {
            PlayerLoop.SetPlayerLoop(new PlayerLoopSystem { type = typeof(ForeignMarker), subSystemList = Array.Empty<PlayerLoopSystem>() });
            Assert.Throws<InvalidOperationException>(() => IrisFrames.Install());
            Assert.That(IrisFrames.Installed, Is.False);
            Assert.That(PlayerLoop.GetCurrentPlayerLoop().type, Is.EqualTo(typeof(ForeignMarker)));
        }

        [Test]
        public void FrameCallbacksReachTheLinkedIrisAndRespectRemoval()
        {
            int updates = 0,
                fixedFrames = 0,
                late = 0,
                isolated = 0;
            using var bus = new EventBus();
            bus.Subscribe<UpdateFrame>(_ => isolated++);
            IrisFrames.Install();
            var update = Iris.Subscribe<UpdateFrame>(_ => updates++);
            using var fixedToken = Iris.Subscribe<FixedFrame>(_ => fixedFrames++);
            using var lateToken = Iris.Subscribe<LateFrame>(_ => late++);
            DriveMarkers(PlayerLoop.GetCurrentPlayerLoop());
            update.Dispose();
            DriveMarkers(PlayerLoop.GetCurrentPlayerLoop());
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(fixedFrames, Is.EqualTo(2));
            Assert.That(late, Is.EqualTo(2));
            Assert.That(isolated, Is.Zero);
        }

        [Test]
        public void MarkersDoNothingWhenIrisIsNotLinked()
        {
            int updates = 0;
            IrisFrames.Install();
            Iris.Subscribe<UpdateFrame>(_ => updates++);
            var drive = FindUpdate(PlayerLoop.GetCurrentPlayerLoop());
            runtime.Dispose();
            Assert.DoesNotThrow(() => drive());
            Assert.That(updates, Is.Zero);
        }

        [Test]
        public void WarmInstalledFrameDeliveryDoesNotAllocate()
        {
            IrisFrames.Install();
            using var token = Iris.Subscribe<UpdateFrame>(_ => { });
            var drive = FindUpdate(PlayerLoop.GetCurrentPlayerLoop());
            Assert.That(drive, Is.Not.Null);
            for (int i = 0; i < 1000; i++)
                drive();
            using var measurement = AllocationMeasurement.Start();
            for (int i = 0; i < 10000; i++)
                drive();
            measurement.AssertNoAllocations();
        }

        private static int Count(PlayerLoopSystem node)
        {
            int result = 1;
            if (node.subSystemList != null)
                foreach (var child in node.subSystemList)
                    result += Count(child);
            return result;
        }

        private static PlayerLoopSystem.UpdateFunction FindUpdate(PlayerLoopSystem node)
        {
            if (node.type?.DeclaringType == typeof(IrisFrames) && node.type.Name == "UpdateMarker")
                return node.updateDelegate;
            if (node.subSystemList != null)
                foreach (var child in node.subSystemList)
                {
                    var found = FindUpdate(child);
                    if (found != null)
                        return found;
                }
            return null;
        }

        private static void DriveMarkers(PlayerLoopSystem node)
        {
            if (node.type?.DeclaringType == typeof(IrisFrames))
                node.updateDelegate?.Invoke();
            if (node.subSystemList != null)
                foreach (var child in node.subSystemList)
                    DriveMarkers(child);
        }
    }
}
