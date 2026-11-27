namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class State
    {
        public int Coins;
        public string[] Transactions = Array.Empty<string>();
    }

    public sealed class StateCodec : ISnapshotCodec<State>
    {
        public byte[] Encode(State state)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(state.Coins);
            writer.Write(state.Transactions.Length);
            foreach (string key in state.Transactions)
                writer.Write(key);
            return stream.ToArray();
        }

        public State Decode(ReadOnlyMemory<byte> payload)
        {
            using var stream = new MemoryStream(payload.ToArray(), false);
            using var reader = new BinaryReader(stream);
            var result = new State { Coins = reader.ReadInt32() };
            int count = reader.ReadInt32();
            if (count < 0 || count > 100)
                throw new FormatException();
            result.Transactions = new string[count];
            for (int i = 0; i < count; i++)
                result.Transactions[i] = reader.ReadString();
            if (stream.Position != stream.Length)
                throw new FormatException();
            return result;
        }
    }

    /// <summary>Contract test double only; no native cloud/storefront capability evidence.</summary>
    public sealed class CloudDouble : ICloudStorage
    {
        private readonly Dictionary<string, List<CloudSaveVersion>> versions =
            new Dictionary<string, List<CloudSaveVersion>>();
        public UniTaskCompletionSource<bool> PublicationGate;
        public readonly UniTaskCompletionSource<bool> PublicationStarted =
            new UniTaskCompletionSource<bool>();
        public bool ExposesDivergentVersions => true;

        private static string Key(CloudAddress address) =>
            address.Account.ToString() + "/" + address.Slot;

        public UniTask<PlatformResult<CloudSaveVersion[]>> ReadVersionsAsync(
            CloudAddress address,
            CancellationToken token
        ) =>
            UniTask.FromResult(
                PlatformResult<CloudSaveVersion[]>.Success(
                    versions.TryGetValue(Key(address), out var list)
                        ? list.ToArray()
                        : Array.Empty<CloudSaveVersion>()
                )
            );

        public async UniTask<PlatformResult<CloudSaveVersion>> PublishAsync(
            CloudAddress address,
            CloudSaveVersion candidate,
            string expected,
            CancellationToken token
        )
        {
            PublicationStarted.TrySetResult(true);
            if (PublicationGate != null)
                await PublicationGate.Task; // Deliberately ignores cancellation to test late native completion.
            if (!versions.TryGetValue(Key(address), out var list))
                versions.Add(Key(address), list = new List<CloudSaveVersion>());
            string current = list.Count == 0 ? null : list[list.Count - 1].Version;
            if (current != expected)
                return PlatformResult<CloudSaveVersion>.Failure(PlatformError.Conflict);
            var version = new CloudSaveVersion(
                Guid.NewGuid().ToString("N"),
                candidate.Export(),
                candidate.LocalRevision,
                candidate.Title
            );
            list.Add(version);
            return PlatformResult<CloudSaveVersion>.Success(version);
        }
    }

    public sealed class CommerceDouble : ICommerce
    {
        public int Acknowledgments;
        public bool FailAcknowledgment;

        public UniTask<PlatformResult<Unit>> AcknowledgeAsync(
            PurchaseReceipt receipt,
            CancellationToken token
        )
        {
            Acknowledgments++;
            return UniTask.FromResult(
                FailAcknowledgment
                    ? PlatformResult<Unit>.Failure(PlatformError.NativeFailure)
                    : PlatformResult<Unit>.Success(default)
            );
        }

        public UniTask<PlatformResult<CatalogItem[]>> ReadCatalogAsync(CancellationToken token) =>
            UniTask.FromResult(PlatformResult<CatalogItem[]>.Success(Array.Empty<CatalogItem>()));

        public UniTask<PlatformResult<PurchaseReceipt>> PurchaseAsync(
            ProductId product,
            CancellationToken token
        ) => UniTask.FromResult(PlatformResult<PurchaseReceipt>.Failure(PlatformError.Unsupported));

        public UniTask<PlatformResult<PurchaseReceipt[]>> RestorePurchasesAsync(CancellationToken token) =>
            UniTask.FromResult(
                PlatformResult<PurchaseReceipt[]>.Success(Array.Empty<PurchaseReceipt>())
            );
    }

    public class PersistenceIntegrationTests
    {
        private string root;
        private AccountSession accounts;
        private AccountLocalStores stores;
        private SnapshotSchema<State> schema;

        [SetUp]
        public void Setup()
        {
            root = Path.Combine(
                Application.temporaryCachePath,
                "ThreadlinkPlatform",
                Guid.NewGuid().ToString("N")
            );
            accounts = new AccountSession();
            accounts.Select(new AccountIdentity("local", "a"));
            stores = AccountLocalStores.FileSystem(accounts, root);
            schema = new SnapshotSchema<State>(
                new Guid("9f32f878-8f14-47cf-b100-33a367b5a984"),
                1,
                new StateCodec(),
                value =>
                {
                    if (value == null || value.Coins < 0 || value.Transactions == null)
                        throw new ArgumentException();
                }
            );
        }

        [TearDown]
        public void Cleanup()
        {
            stores.Dispose();
            accounts.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private static IEnumerator Run(Func<UniTask> action)
        {
            var task = action();
            while (!task.Status.IsCompleted())
                yield return null;
            task.GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator AccountChangesInvalidateWorkAndNeverMoveOrShareProviderData() =>
            Run(async () =>
            {
                var first = stores.Open(schema);
                Assert.That(
                    (await first.Store.SaveAsync(1, new State { Coins = 7 }, "a")).Succeeded,
                    Is.True
                );
                accounts.Select(new AccountIdentity("steam", "a"));
                Assert.That(first.Account.IsCurrent, Is.False);
                Assert.That(
                    (await first.Store.SaveAsync(1, new State { Coins = 9 }, "stale")).Succeeded,
                    Is.False
                );
                var second = stores.Open(schema);
                Assert.That(
                    (await second.Store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.NotFound)
                );
                Assert.That(
                    AccountLocalStores.AccountDirectory(root, first.Account.Account),
                    Is.Not.EqualTo(
                        AccountLocalStores.AccountDirectory(root, second.Account.Account)
                    )
                );
                accounts.Select(new AccountIdentity("local", "a"));
                Assert.That(
                    (await stores.Open(schema).Store.ReadAsync(1)).Value.Value.Coins,
                    Is.EqualTo(7)
                );
            });

        [UnityTest]
        public IEnumerator CloudConflictPreservesLocalAndRemoteVersionsUntilExplicitChoice() =>
            Run(async () =>
            {
                var local = stores.Open(schema);
                var cloud = new CloudDouble();
                var sync = new CloudSynchronization<State>(local, cloud);
                await local.Store.SaveAsync(1, new State { Coins = 7 }, "forest");
                Assert.That(
                    (await sync.UploadAsync(1, null)).State,
                    Is.EqualTo(SynchronizationState.Uploaded)
                );
                var firstRemote = (await sync.FetchAsync(1)).Value[0];
                await local.Store.SaveAsync(1, new State { Coins = 8 }, "grove");
                Assert.That(
                    (await sync.UploadAsync(1, firstRemote.Version)).State,
                    Is.EqualTo(SynchronizationState.Uploaded)
                );
                var localRevision = (
                    await local.Store.SaveAsync(1, new State { Coins = 9 }, "local")
                )
                    .Value
                    .Revision;
                var conflict = await sync.UploadAsync(1, firstRemote.Version);
                Assert.That(conflict.State, Is.EqualTo(SynchronizationState.Conflict));
                Assert.That(conflict.Conflicts.Count, Is.EqualTo(2));
                Assert.That((await local.Store.ReadAsync(1)).Value.Value.Coins, Is.EqualTo(9));
                Assert.That(sync.Preview(conflict.Conflicts[0]).Value.Coins, Is.EqualTo(7));
                Assert.That(
                    (await sync.AcceptRemoteAsync(1, conflict.Conflicts[0], Guid.Empty)).Error,
                    Is.EqualTo(PersistenceError.Conflict)
                );
                Assert.That(
                    (
                        await sync.AcceptRemoteAsync(1, conflict.Conflicts[0], localRevision)
                    ).Succeeded,
                    Is.True
                );
                Assert.That((await local.Store.ReadAsync(1)).Value.Value.Coins, Is.EqualTo(7));
                Assert.That((await sync.FetchAsync(1)).Value.Length, Is.EqualTo(2));
                byte[] invalid = firstRemote.Export();
                invalid[invalid.Length - 1] ^= 1;
                Assert.That(
                    sync.Preview(new CloudSaveVersion("bad", invalid, Guid.NewGuid(), "bad")).Error,
                    Is.EqualTo(PersistenceError.CorruptData)
                );
            });

        [UnityTest]
        public IEnumerator AccountSwitchDuringRemotePublicationReportsAmbiguityWithoutLosingLocalCommit() =>
            Run(async () =>
            {
                var local = stores.Open(schema);
                var cloud = new CloudDouble { PublicationGate = new UniTaskCompletionSource<bool>() };
                var sync = new CloudSynchronization<State>(local, cloud);
                var saved = await local.Store.SaveAsync(1, new State { Coins = 7 }, "saved");
                var upload = sync.UploadAsync(1, null);
                await cloud.PublicationStarted.Task;
                accounts.Select(new AccountIdentity("local", "b"));
                cloud.PublicationGate.TrySetResult(true);
                var result = await upload;
                Assert.That(result.LocalRevision, Is.EqualTo(saved.Value.Revision));
                Assert.That(result.State, Is.EqualTo(SynchronizationState.Cancelled));
                Assert.That(result.MayHaveCommittedRemotely, Is.True);
                Assert.That(
                    (await stores.Open(schema).Store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.NotFound)
                );
                accounts.Select(new AccountIdentity("local", "a"));
                Assert.That(
                    (await stores.Open(schema).Store.ReadAsync(1)).Value.Value.Coins,
                    Is.EqualTo(7)
                );
            });

        [UnityTest]
        public IEnumerator DuplicateTransactionsAfterRestartRetryAcknowledgmentWithoutDoubleGrant() =>
            Run(async () =>
            {
                int grants = 0;
                var commerce = new CommerceDouble { FailAcknowledgment = true };
                var receipt = new PurchaseReceipt(
                    accounts.Current.Value,
                    "native-1",
                    new ProductId("coins"),
                    Acknowledgment.Pending
                );
                PurchaseFulfillment<State> Flow() =>
                    new PurchaseFulfillment<State>(
                        stores.Open(schema),
                        commerce,
                        () => new State(),
                        state => state.Transactions,
                        (state, purchase) =>
                        {
                            grants++;
                            return new State
                            {
                                Coins = state.Coins + 10,
                                Transactions = state
                                    .Transactions.Concat(new[] { purchase.StableKey })
                                    .ToArray(),
                            };
                        }
                    );
                var first = await Flow().ApplyAsync(3, receipt);
                Assert.That(first.LocallyCommitted, Is.True);
                Assert.That(first.Acknowledgment.Succeeded, Is.False);
                stores.Dispose();
                stores = AccountLocalStores.FileSystem(accounts, root);
                commerce.FailAcknowledgment = false;
                var repeated = await Flow().ApplyAsync(3, receipt);
                Assert.That(repeated.AlreadyFulfilled, Is.True);
                Assert.That(repeated.Acknowledgment.Succeeded, Is.True);
                Assert.That(repeated.State.Coins, Is.EqualTo(10));
                Assert.That(grants, Is.EqualTo(1));
                Assert.That(commerce.Acknowledgments, Is.EqualTo(2));
            });

        [UnityTest]
        public IEnumerator WrongAccountReceiptAndUnrecordedFulfillmentCannotCommit() =>
            Run(async () =>
            {
                var flow = new PurchaseFulfillment<State>(
                    stores.Open(schema),
                    new CommerceDouble(),
                    () => new State(),
                    state => state.Transactions,
                    (state, receipt) => new State { Coins = 10 }
                );
                var other = new PurchaseReceipt(
                    new AccountIdentity("local", "other"),
                    "native",
                    new ProductId("coins"),
                    Acknowledgment.Pending
                );
                Assert.That((await flow.ApplyAsync(1, other)).LocallyCommitted, Is.False);
                bool rejected = false;
                try
                {
                    await flow.ApplyAsync(
                        1,
                        new PurchaseReceipt(
                            accounts.Current.Value,
                            "native",
                            new ProductId("coins"),
                            Acknowledgment.Pending
                        )
                    );
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                Assert.That(rejected, Is.True);
                Assert.That(
                    (await stores.Open(schema).Store.ReadAsync(1)).Error,
                    Is.EqualTo(PersistenceError.NotFound)
                );
            });
    }
}
