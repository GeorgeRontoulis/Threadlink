namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public sealed class FulfillmentOutcome<T>
    {
        internal FulfillmentOutcome() { }

        public bool LocallyCommitted { get; internal set; }
        public bool AlreadyFulfilled { get; internal set; }
        public T State { get; internal set; }
        public PersistenceError LocalError { get; internal set; }
        public PlatformResult<Unit> Acknowledgment { get; internal set; }
    }

    /// <summary>Atomic snapshot-plus-ledger fulfillment. Game mapping returns data only; apply live-game state after successful local commitment.</summary>
    public sealed class PurchaseFulfillment<T>
    {
        private readonly AccountSnapshot<T> local;
        private readonly ICommerce commerce;
        private readonly Func<T> empty;
        private readonly Func<T, IReadOnlyCollection<string>> ledger;
        private readonly Func<T, PurchaseReceipt, T> fulfill;

        public PurchaseFulfillment(
            AccountSnapshot<T> local,
            ICommerce commerce,
            Func<T> empty,
            Func<T, IReadOnlyCollection<string>> ledger,
            Func<T, PurchaseReceipt, T> fulfill
        )
        {
            this.local = local ?? throw new ArgumentNullException(nameof(local));
            this.commerce = commerce ?? throw new ArgumentNullException(nameof(commerce));
            this.empty = empty ?? throw new ArgumentNullException(nameof(empty));
            this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            this.fulfill = fulfill ?? throw new ArgumentNullException(nameof(fulfill));
        }

        public async UniTask<FulfillmentOutcome<T>> ApplyAsync(
            int slot,
            PurchaseReceipt receipt,
            CancellationToken token = default
        )
        {
            var outcome = new FulfillmentOutcome<T>
            {
                Acknowledgment = PlatformResult<Unit>.Failure(
                    PlatformError.Unavailable,
                    "No local fulfillment committed."
                ),
            };
            if (
                receipt == null
                || !local.Account.IsCurrent
                || receipt.Account != local.Account.Account
            )
            {
                outcome.LocalError = PersistenceError.AccountUnavailable;
                return outcome;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                local.Account.Token
            );
            var read = await local.Store.ReadAsync(slot, linked.Token);
            if (!read.Succeeded && read.Error != PersistenceError.NotFound)
            {
                outcome.LocalError = read.Error;
                return outcome;
            }
            T state = read.Succeeded ? read.Value.Value : empty();
            var entries =
                ledger(state)
                ?? throw new InvalidOperationException("Fulfillment ledger is required.");
            bool already = Contains(entries, receipt.StableKey);
            outcome.AlreadyFulfilled = already;
            if (!already)
            {
                var previousEntries = new List<string>(entries);
                state = fulfill(state, receipt);
                var updated = ledger(state);
                if (updated == null || !Contains(updated, receipt.StableKey))
                    throw new InvalidOperationException(
                        "Fulfillment must record its stable transaction key in the same snapshot."
                    );
                foreach (string previous in previousEntries)
                    if (!Contains(updated, previous))
                        throw new InvalidOperationException(
                            "Fulfillment cannot drop earlier transaction identities."
                        );
                local.Account.ThrowIfInvalid();
                var saved = await local.Store.SaveAsync(
                    slot,
                    state,
                    "Purchases",
                    read.Succeeded ? read.Value.Summary.Revision : Guid.Empty,
                    linked.Token
                );
                if (!saved.Succeeded)
                {
                    outcome.LocalError = saved.Error;
                    return outcome;
                }
            }
            outcome.LocallyCommitted = true;
            outcome.State = state;
            if (!local.Account.IsCurrent)
            {
                outcome.Acknowledgment = PlatformResult<Unit>.Failure(PlatformError.AccountChanged);
                return outcome;
            }
            if (receipt.Acknowledgment != Acknowledgment.Pending)
            {
                outcome.Acknowledgment = PlatformResult<Unit>.Success(default);
                return outcome;
            }
            try
            {
                outcome.Acknowledgment = await commerce.AcknowledgeAsync(receipt, linked.Token);
            }
            catch (OperationCanceledException)
            {
                outcome.Acknowledgment = PlatformResult<Unit>.Failure(
                    local.Account.IsCurrent
                        ? PlatformError.Cancelled
                        : PlatformError.AccountChanged,
                    mayHaveCommitted: true
                );
            }
            catch (Exception error)
            {
                outcome.Acknowledgment = PlatformResult<Unit>.Failure(
                    PlatformError.NativeFailure,
                    error.Message,
                    true
                );
            }
            if (!local.Account.IsCurrent)
                outcome.Acknowledgment = PlatformResult<Unit>.Failure(
                    PlatformError.AccountChanged,
                    mayHaveCommitted: true
                );
            return outcome;
        }

        private static bool Contains(IReadOnlyCollection<string> values, string key)
        {
            foreach (string value in values)
                if (value == key)
                    return true;
            return false;
        }
    }
}
