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
    }
}
