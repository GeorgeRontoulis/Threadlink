namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System.Collections;
    using MessagePack;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    [Save("threadlink-tests-sentinel-saves", 1)]
    [MessagePackObject]
    public sealed partial class SentinelTestSave
    {
        [Key(0)]
        public int Value { get; set; }
    }

    public sealed class SentinelSavesTests
    {
        private static IEnumerator Wait<T>(UniTask<T> running, System.Action<T> done)
        {
            var awaiter = running.GetAwaiter();
            while (!awaiter.IsCompleted)
                yield return null;
            done(awaiter.GetResult());
        }

        [UnityTest]
        public IEnumerator SlotsAreNumberedFromZeroAndConflictsAreDetected()
        {
            PersistenceResult deleted = default;
            yield return Wait(Sentinel.Saves.DeleteAsync<SentinelTestSave>(0), result => deleted = result);
            PersistenceResult<SlotSummary> saved = default;
            yield return Wait(Sentinel.Saves.SaveAsync(0, new SentinelTestSave { Value = 7 }), result => saved = result);
            Assert.That(saved.Succeeded, Is.True, saved.Message);
            Assert.That(Sentinel.Saves.List<SentinelTestSave>(), Has.Member(0));
            PersistenceResult<SentinelTestSave> loaded = default;
            yield return Wait(Sentinel.Saves.LoadAsync<SentinelTestSave>(0), result => loaded = result);
            Assert.That(loaded.Succeeded && loaded.Value.Value == 7, Is.True);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Sentinel.Saves.LoadAsync<SentinelTestSave>(-1));
            yield return Wait(Sentinel.Saves.DeleteAsync<SentinelTestSave>(0), result => deleted = result);
            Assert.That(deleted.Succeeded, Is.True);
        }

        [UnityTest]
        public IEnumerator WritesFailuresAndRecoveriesAreAnnounced()
        {
            var heard = new System.Collections.Generic.List<string>();
            var tokens = new[]
            {
                Iris.Subscribe<SaveWritten>(e => heard.Add("written " + e.Slot + " " + e.Save.Name)),
                Iris.Subscribe<SaveFailed>(e => heard.Add("failed " + e.Slot + " " + e.Operation + " " + e.Error)),
                Iris.Subscribe<SaveRecovered>(e => heard.Add("recovered " + e.Slot)),
            };
            try
            {
                PersistenceResult deleted = default;
                yield return Wait(Sentinel.Saves.DeleteAsync<SentinelTestSave>(1), result => deleted = result);
                PersistenceResult<SentinelTestSave> missing = default;
                yield return Wait(Sentinel.Saves.LoadAsync<SentinelTestSave>(1), result => missing = result);
                Assert.That(missing.Error, Is.EqualTo(PersistenceError.NotFound));
                Assert.That(heard, Is.Empty, "An empty slot is not a failure.");
                PersistenceResult<SlotSummary> saved = default;
                yield return Wait(Sentinel.Saves.SaveAsync(1, new SentinelTestSave { Value = 1 }), result => saved = result);
                yield return Wait(Sentinel.Saves.SaveAsync(1, new SentinelTestSave { Value = 2 }), result => saved = result);
                Assert.That(saved.Succeeded, Is.True, saved.Message);
                PersistenceResult restored = default;
                yield return Wait(Sentinel.Saves.RestorePreviousAsync<SentinelTestSave>(1), result => restored = result);
                Assert.That(restored.Succeeded, Is.True, restored.Message);
                Assert.That(heard, Is.EqualTo(new[] { "written 1 SentinelTestSave", "written 1 SentinelTestSave", "recovered 1" }));
            }
            finally
            {
                foreach (var token in tokens)
                    token.Dispose();
            }
            PersistenceResult cleaned = default;
            yield return Wait(Sentinel.Saves.DeleteAsync<SentinelTestSave>(1), result => cleaned = result);
        }
    }
}
