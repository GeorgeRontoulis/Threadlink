namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using global::Unity.Profiling;
    using NUnit.Framework;

    internal sealed class AllocationMeasurement : IDisposable
    {
        private ProfilerRecorder recorder;
        private long before;
        private bool useCounter;
        private static bool counterChecked,
            counterAvailable;
        internal string Unit => useCounter ? "bytes" : "GC.Alloc events";

        internal static AllocationMeasurement Start()
        {
            var measurement = new AllocationMeasurement();
            if (!counterChecked)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var counterControl = new byte[1024 * 1024];
                long delta = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(counterControl);
                counterAvailable = delta >= counterControl.Length;
                counterChecked = true;
            }
            measurement.useCounter = counterAvailable;
            if (!measurement.useCounter)
            {
                measurement.recorder = new ProfilerRecorder(
                    ProfilerCategory.Memory,
                    "GC.Alloc",
                    1,
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached
                        | ProfilerRecorderOptions.SumAllSamplesInFrame
                        | ProfilerRecorderOptions.CollectOnlyOnCurrentThread
                );
                if (!measurement.recorder.Valid)
                {
                    measurement.Dispose();
                    Assert.Inconclusive("No valid allocation instrument on this backend.");
                }
                measurement.recorder.Start();
                var control = new byte[64];
                measurement.recorder.Stop();
                GC.KeepAlive(control);
                if (measurement.recorder.Count == 0 || measurement.recorder.GetSample(0).Count == 0)
                {
                    measurement.Dispose();
                    Assert.Inconclusive(
                        "The allocation instruments failed their retained positive controls."
                    );
                }
                measurement.recorder.Reset();
                // JIT/assertion setup must happen while stopped, outside the measured operation.
                measurement.AssertNoAllocations();
                measurement.recorder.Start();
            }
            measurement.before = GC.GetAllocatedBytesForCurrentThread();
            return measurement;
        }

        internal void AssertNoAllocations()
        {
            Assert.That(Finish(), Is.Zero, useCounter ? "Allocated bytes" : "GC.Alloc events");
        }

        internal long Finish()
        {
            long value;
            if (useCounter)
                value = GC.GetAllocatedBytesForCurrentThread() - before;
            else
            {
                recorder.Stop();
                value = recorder.Count > 0 ? recorder.GetSample(0).Count : 0;
            }
            return value;
        }

        public void Dispose() => recorder.Dispose();
    }
}
