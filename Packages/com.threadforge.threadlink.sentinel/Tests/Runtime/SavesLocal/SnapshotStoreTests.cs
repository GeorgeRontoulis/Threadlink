namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.IO;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public class SnapshotStoreTests
    {
        private string root;
        private LocalSaveService native;
        private SnapshotStore<int> store;

        private sealed class Codec : ISnapshotCodec<int>
        {
            public byte[] Encode(int value) => BitConverter.GetBytes(value);

            public int Decode(ReadOnlyMemory<byte> bytes) =>
                bytes.Length == 4
                    ? BitConverter.ToInt32(bytes.ToArray(), 0)
                    : throw new FormatException();
        }

        [SetUp]
        public void Setup()
        {
            root = Path.Combine(
                Application.temporaryCachePath,
                "ThreadlinkPersistence",
                Guid.NewGuid().ToString("N")
            );
            native = new LocalSaveService(root);
            store = new SnapshotStore<int>(
                native,
                new SnapshotSchema<int>(
                    Guid.NewGuid(),
                    1,
                    new Codec(),
                    value =>
                    {
                        if (value < 0)
                            throw new ArgumentException();
                    }
                )
            );
        }

        [TearDown]
        public void Cleanup()
        {
            native.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private IEnumerator Run(Func<UniTask> action) => LocalSaveTests.Run(action);

        [UnityTest]
        public IEnumerator SlotsSummariesCompareAndSaveAndLogicalDelete() =>
            Run(async () =>
            {
                var saved = await store.SaveAsync(1, 7, "Forest", Guid.Empty);
                Assert.That(saved.Succeeded, Is.True, saved.Message);
                Assert.That((await store.ReadAsync(1)).Value.Summary.Title, Is.EqualTo("Forest"));
                Assert.That(store.ListSlots().Value, Is.EqualTo(new[] { 1 }));
                Assert.That(
                    (await store.SaveAsync(1, 8, "stale", Guid.Empty)).Error,
                    Is.EqualTo(PersistenceError.Conflict)
                );
                Assert.That(
                    (await store.SaveAsync(1, -1, "invalid")).Error,
                    Is.EqualTo(PersistenceError.InvalidArgument)
                );
                Assert.That(
                    (await store.SaveAsync(1, 8, "Grove", saved.Value.Revision)).Succeeded,
                    Is.True
                );
                Assert.That((await store.DeleteAsync(1)).Succeeded, Is.True);
                Assert.That(
                    (await store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.NotFound)
                );
                Assert.That(store.ListRecoveryVersions(1).Value.Length, Is.EqualTo(2));
            });

        [UnityTest]
        public IEnumerator ChecksumCorruptionPreservesPublishedBytesAndExplicitRecoveryValidates() =>
            Run(async () =>
            {
                await store.SaveAsync(1, 7, "safe");
                string first = File.ReadAllText(Path.Combine(root, "1/current.ptr"));
                await store.SaveAsync(1, 8, "new");
                string second = File.ReadAllText(Path.Combine(root, "1/current.ptr"));
                string path = Path.Combine(root, "1/generations", second, "1.sent");
                byte[] corrupt = File.ReadAllBytes(path);
                corrupt[corrupt.Length - 1] ^= 1;
                File.WriteAllBytes(path, corrupt);
                Assert.That(
                    (await store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That(
                    (await store.SaveAsync(1, 9, "blocked")).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That((await store.RecoverAsync(1, second)).Succeeded, Is.False);
                Assert.That(
                    File.ReadAllText(Path.Combine(root, "1/current.ptr")),
                    Is.EqualTo(second)
                );
                Assert.That((await store.RecoverAsync(1, first)).Succeeded, Is.True);
                Assert.That((await store.ReadAsync(1)).Value.Value, Is.EqualTo(7));
            });

        [UnityTest]
        public IEnumerator CorruptPointerAndMissingSnapshotRequireRecoveryRatherThanOverwrite() =>
            Run(async () =>
            {
                await store.SaveAsync(1, 7, "safe");
                string pointer = Path.Combine(root, "1/current.ptr");
                string generation = File.ReadAllText(pointer);
                File.WriteAllText(pointer, "invalid");
                Assert.That(
                    (await store.SaveAsync(1, 9, "blocked")).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That((await store.RecoverAsync(1, generation)).Succeeded, Is.True);
                File.Delete(Path.Combine(root, "1/generations", generation, "1.sent"));
                Assert.That(
                    (await store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
                Assert.That(
                    (await store.SaveAsync(1, 9, "blocked")).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
            });

        [UnityTest]
        public IEnumerator CancellationAndUnpublishedStagingDoNotReplaceCurrentSave() =>
            Run(async () =>
            {
                var saved = await store.SaveAsync(1, 7, "safe");
                var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                bool cancelled = false;
                try
                {
                    await store.SaveAsync(1, 9, "cancelled", token: cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                Assert.That(cancelled, Is.True);
                string abandoned = Path.Combine(root, ".staging/1-interrupted");
                Directory.CreateDirectory(abandoned);
                File.WriteAllBytes(Path.Combine(abandoned, "1.sent"), new byte[] { 1 });
                Assert.That(
                    (await store.ReadAsync(1)).Value.Summary.Revision,
                    Is.EqualTo(saved.Value.Revision)
                );
                cancellation.Dispose();
            });

        [UnityTest]
        public IEnumerator StorageByteLimitRejectsExcessiveWritesAndReads() =>
            Run(async () =>
            {
                using var limited = new LocalSaveService(root, 4);
                using var tx = (await limited.BeginTransactionAsync(2)).Value;
                Assert.That(
                    (await tx.WriteAsync(1, new byte[5])).Error,
                    Is.EqualTo(PersistenceError.InvalidArgument)
                );
                Assert.That((await tx.WriteAsync(1, new byte[4])).Succeeded, Is.True);
                Assert.That((await tx.CommitAsync()).Succeeded, Is.True);
                string generation = File.ReadAllText(Path.Combine(root, "2/current.ptr"));
                File.WriteAllBytes(
                    Path.Combine(root, "2/generations", generation, "1.sent"),
                    new byte[5]
                );
                Assert.That((await limited.ReadAsync(2, 1)).Succeeded, Is.False);
            });
    }
}
