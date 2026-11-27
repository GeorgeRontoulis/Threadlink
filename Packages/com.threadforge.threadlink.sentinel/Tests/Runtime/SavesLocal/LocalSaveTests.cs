namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.IO;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class LocalSaveTests
    {
        internal static IEnumerator Run(Func<UniTask> action)
        {
            var task = action();
            while (!task.Status.IsCompleted())
                yield return null;
            task.GetAwaiter().GetResult();
        }

        private string root;
        private LocalSaveService service;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Application.temporaryCachePath,
                "ThreadlinkFrameworkTests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(root);
            service = new LocalSaveService(root);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            if (!Directory.Exists(root))
                return;
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, true);
        }

        [Test]
        public void StorePreservesAbsoluteVolumeRootAndNormalizesDirectorySeparators()
        {
            string volumeRoot = Path.GetPathRoot(Path.GetFullPath(root));
            using var volumeStore = new LocalSaveStore(volumeRoot);
            Assert.That(volumeStore.GetSaveDirectory(7), Is.EqualTo(Path.Combine(volumeRoot, "7")));
            using var directoryStore = new LocalSaveStore(root + Path.DirectorySeparatorChar);
            Assert.That(
                directoryStore.GetSaveDirectory(7),
                Is.EqualTo(Path.Combine(Path.GetFullPath(root), "7"))
            );
        }

        private async UniTask<ISaveTransaction> Begin()
        {
            var result = await service.BeginTransactionAsync(7);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private async UniTask Seed()
        {
            var tx = await Begin();
            try
            {
                await tx.WriteAsync(1, new byte[] { 1, 2, 3 });
                Assert.That((await tx.CommitAsync()).Succeeded, Is.True);
            }
            finally
            {
                tx.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RoundTripSnapshotsAndConflicts() =>
            Run(async () =>
            {
                await Seed();
                var a = await Begin();
                var b = await Begin();
                try
                {
                    byte[] data = { 9 };
                    await a.WriteAsync(1, data);
                    data[0] = 0;
                    Assert.That((await a.CommitAsync()).Succeeded, Is.True);
                    Assert.That((await b.ReadAsync(1)).Value, Is.EqualTo(new byte[] { 1, 2, 3 }));
                    await b.WriteAsync(2, new byte[] { 8 });
                    Assert.That(
                        (await b.CommitAsync()).Error,
                        Is.EqualTo(PersistenceError.Conflict)
                    );
                    Assert.That(
                        (await service.ReadAsync(7, 1)).Value,
                        Is.EqualTo(new byte[] { 9 })
                    );
                    Assert.That(
                        (await service.ReadAsync(7, 2)).Error,
                        Is.EqualTo(PersistenceError.NotFound)
                    );
                }
                finally
                {
                    a.Dispose();
                    b.Dispose();
                }
            });

        [UnityTest]
        public IEnumerator FileAndSaveDeletionPublishCompleteState() =>
            Run(async () =>
            {
                await Seed();
                var tx = await Begin();
                try
                {
                    await tx.DeleteAsync(1);
                    await tx.WriteAsync(2, new byte[] { 4 });
                    Assert.That((await tx.CommitAsync()).Succeeded, Is.True);
                    Assert.That(
                        (await service.ReadAsync(7, 1)).Error,
                        Is.EqualTo(PersistenceError.NotFound)
                    );
                    Assert.That(
                        (await service.ReadAsync(7, 2)).Value,
                        Is.EqualTo(new byte[] { 4 })
                    );
                    Assert.That((await service.DeleteSaveAsync(7)).Succeeded, Is.True);
                    Assert.That(
                        (await service.ReadAsync(7, 2)).Error,
                        Is.EqualTo(PersistenceError.NotFound)
                    );
                }
                finally
                {
                    tx.Dispose();
                }
            });

        [UnityTest]
        public IEnumerator FailedDeletionDoesNotPublish() =>
            Run(async () =>
            {
                if (
                    Application.platform != RuntimePlatform.WindowsEditor
                    && Application.platform != RuntimePlatform.WindowsPlayer
                )
                    Assert.Ignore("Windows read-only-file fault injection.");
                await Seed();
                string pointer = Path.Combine(root, "7", "current.ptr");
                string original = File.ReadAllText(pointer);
                string file = Path.Combine(root, "7", "generations", original, "1.sent");
                File.SetAttributes(file, FileAttributes.ReadOnly);
                var tx = await Begin();
                try
                {
                    await tx.DeleteAsync(1);
                    var result = await tx.CommitAsync();
                    Assert.That(result.Succeeded, Is.False);
                    Assert.That(tx.IsCommitted, Is.False);
                    Assert.That(File.ReadAllText(pointer), Is.EqualTo(original));
                    Assert.That(
                        (await service.ReadAsync(7, 1)).Value,
                        Is.EqualTo(new byte[] { 1, 2, 3 })
                    );
                }
                finally
                {
                    tx.Dispose();
                    File.SetAttributes(file, FileAttributes.Normal);
                }
            });

        [UnityTest]
        public IEnumerator DisposalBeforePublicationPreservesPreviousGeneration() =>
            Run(async () =>
            {
                await Seed();
                var tx = await Begin();
                var store = (LocalSaveStore)
                    typeof(LocalSaveService)
                        .GetProperty("Store", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(service);
                var gate = store.GetPublishLock(7);
                await gate.WaitAsync();
                try
                {
                    await tx.WriteAsync(1, new byte[8192]);
                    var pending = tx.CommitAsync();
                    Assert.That((await tx.CommitAsync()).Error, Is.EqualTo(PersistenceError.Busy));
                    Assert.That(
                        (await tx.WriteAsync(2, new byte[] { 1 })).Error,
                        Is.EqualTo(PersistenceError.Busy)
                    );
                    service.Dispose();
                    tx.Dispose();
                    gate.Release();
                    Assert.That((await pending).Succeeded, Is.False);
                    using var replacement = new LocalSaveService(root);
                    CollectionAssert.AreEqual(
                        new byte[] { 1, 2, 3 },
                        (await replacement.ReadAsync(7, 1)).Value
                    );
                    Assert.That(
                        (await service.BeginTransactionAsync(7)).Error,
                        Is.EqualTo(PersistenceError.NotInitialized)
                    );
                    Assert.That((await tx.ReadAsync(1)).Succeeded, Is.False);
                }
                finally
                {
                    tx.Dispose();
                    if (gate.CurrentCount == 0)
                        gate.Release();
                }
            });

        [UnityTest]
        public IEnumerator InvalidAndDisposedTransactionsReturnFailures() =>
            Run(async () =>
            {
                Assert.That(
                    (await service.BeginTransactionAsync(0)).Error,
                    Is.EqualTo(PersistenceError.InvalidArgument)
                );
                var tx = await Begin();
                Assert.That(
                    (await tx.WriteAsync(0, new byte[0])).Error,
                    Is.EqualTo(PersistenceError.InvalidArgument)
                );
                Assert.That(
                    (await tx.WriteAsync(1, null)).Error,
                    Is.EqualTo(PersistenceError.InvalidArgument)
                );
                tx.Dispose();
                tx.Dispose();
                Assert.That(
                    (await tx.CommitAsync()).Error,
                    Is.EqualTo(PersistenceError.NotInitialized)
                );
                Assert.That((await tx.DeleteAsync(1)).Succeeded, Is.False);
            });

        [UnityTest]
        public IEnumerator CorruptPointerDoesNotOverwritePublishedData() =>
            Run(async () =>
            {
                await Seed();
                string pointer = Path.Combine(root, "7", "current.ptr");
                File.WriteAllText(pointer, "");
                Assert.That(
                    (await service.BeginTransactionAsync(7)).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That(File.ReadAllText(pointer), Is.Empty);
            });

        [UnityTest]
        public IEnumerator HistoryIsOnlyPrunedExplicitlyAndPinsAreRespected() =>
            Run(async () =>
            {
                await Seed();
                var snapshot = await Begin();
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var tx = await Begin();
                        try
                        {
                            await tx.WriteAsync(1, new byte[] { (byte)i });
                            Assert.That((await tx.CommitAsync()).Succeeded, Is.True);
                        }
                        finally
                        {
                            tx.Dispose();
                        }
                    }
                    Assert.That(service.InspectStorage(7).Value.Generations, Is.EqualTo(5));
                    Assert.That(service.PruneHistory(7).Error, Is.EqualTo(PersistenceError.Busy));
                    var secondService = new LocalSaveService(root);
                    try
                    {
                        Assert.That(
                            secondService.PruneHistory(7).Error,
                            Is.EqualTo(PersistenceError.Busy)
                        );
                    }
                    finally
                    {
                        secondService.Dispose();
                    }
                    CollectionAssert.AreEqual(
                        new byte[] { 1, 2, 3 },
                        (await snapshot.ReadAsync(1)).Value
                    );
                }
                finally
                {
                    snapshot.Dispose();
                }
                Assert.That(service.InspectStorage(7).Value.Generations, Is.EqualTo(5));
                var pruned = service.PruneHistory(7);
                Assert.That(pruned.Succeeded, Is.True, pruned.Message);
                Assert.That(pruned.Value, Is.EqualTo(3));
                Assert.That(service.InspectStorage(7).Value.Generations, Is.EqualTo(2));
                CollectionAssert.AreEqual(new byte[] { 3 }, (await service.ReadAsync(7, 1)).Value);
                Assert.That(service.PruneHistory(7).Value, Is.Zero);
            });

        [UnityTest]
        public IEnumerator MaintenancePreservesUnfamiliarDirectoriesAndCorruptPointers() =>
            Run(async () =>
            {
                await Seed();
                string generations = Path.Combine(root, "7", "generations");
                string unrelated = Path.Combine(generations, "user-backup");
                Directory.CreateDirectory(unrelated);
                File.WriteAllText(Path.Combine(unrelated, "note.txt"), "keep");
                string pointer = Path.Combine(root, "7", "current.ptr");
                File.WriteAllText(pointer, "invalid");
                Assert.That(
                    service.PruneHistory(7).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That(
                    File.ReadAllText(Path.Combine(unrelated, "note.txt")),
                    Is.EqualTo("keep")
                );
                Assert.That(Directory.GetDirectories(generations).Length, Is.EqualTo(2));
            });
    }
}
