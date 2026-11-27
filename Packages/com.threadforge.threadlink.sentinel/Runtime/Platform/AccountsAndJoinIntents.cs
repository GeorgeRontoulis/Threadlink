namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>Provider-qualified identity. Matching strings from different providers never imply the same account.</summary>
    public readonly struct AccountIdentity : IEquatable<AccountIdentity>
    {
        public string Provider { get; }
        public string Value { get; }

        public AccountIdentity(string provider, string value)
        {
            Provider = provider;
            Value = value;
            Validate();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Provider) || string.IsNullOrWhiteSpace(Value))
                throw new ArgumentException("Provider and account identity are required.");
        }

        public bool Equals(AccountIdentity other) =>
            Provider == other.Provider && Value == other.Value;

        public override bool Equals(object obj) => obj is AccountIdentity other && Equals(other);

        public override int GetHashCode() =>
            ((Provider?.GetHashCode() ?? 0) * 397) ^ (Value?.GetHashCode() ?? 0);

        public override string ToString() => Provider + ":" + Value;

        public static bool operator ==(AccountIdentity a, AccountIdentity b) => a.Equals(b);

        public static bool operator !=(AccountIdentity a, AccountIdentity b) => !a.Equals(b);
    }

    /// <summary>Explicit game-owned cross-provider links. Link only after the application verifies identity ownership.</summary>
    public sealed class IdentityLinks
    {
        private readonly Dictionary<AccountIdentity, AccountIdentity> links =
            new Dictionary<AccountIdentity, AccountIdentity>();

        public void Link(AccountIdentity gameAccount, AccountIdentity external)
        {
            gameAccount.Validate();
            external.Validate();
            if (links.TryGetValue(external, out var previous) && previous != gameAccount)
                throw new InvalidOperationException(
                    "Explicit unlink is required before reassignment."
                );
            foreach (var pair in links)
                if (
                    pair.Key.Provider == external.Provider
                    && pair.Key != external
                    && pair.Value == gameAccount
                )
                    throw new InvalidOperationException(
                        "This game identity already links a different account from that provider."
                    );
            links[external] = gameAccount;
        }

        public bool TryResolve(AccountIdentity external, out AccountIdentity gameAccount) =>
            links.TryGetValue(external, out gameAccount);

        public bool Unlink(AccountIdentity external) => links.Remove(external);
    }

    /// <summary>Account epoch invalidation. Use on one synchronization context; provider work must observe captured tokens and recheck before publication.</summary>
    public sealed class AccountSession : IDisposable
    {
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        private long epoch;
        private bool disposed;
        public AccountIdentity? Current { get; private set; }
        public Exception ObserverError { get; private set; }
        public event Action Changed;

        public sealed class Lease
        {
            private readonly AccountSession owner;
            private readonly long epoch;
            public AccountIdentity Account { get; }
            public CancellationToken Token { get; }
            public bool IsCurrent => !owner.disposed && owner.epoch == epoch;

            internal Lease(AccountSession owner)
            {
                this.owner = owner;
                epoch = owner.epoch;
                Account = owner.Current.Value;
                Token = owner.cancellation.Token;
            }

            public void ThrowIfInvalid()
            {
                if (!IsCurrent)
                    throw new OperationCanceledException("Account changed.", Token);
                Token.ThrowIfCancellationRequested();
            }
        }

        public Lease Capture()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AccountSession));
            if (!Current.HasValue)
                throw new InvalidOperationException("Account selection is required.");
            return new Lease(this);
        }

        public void Select(AccountIdentity? account)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AccountSession));
            account?.Validate();
            if (Nullable.Equals(Current, account))
                return;
            var previous = cancellation;
            cancellation = new CancellationTokenSource();
            epoch++;
            Current = account;
            try
            {
                previous.Cancel();
            }
            catch (Exception error)
            {
                ObserverError = error;
            }
            finally
            {
                previous.Dispose();
            }
            Notify();
        }

        private void Notify()
        {
            var handlers = Changed;
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
                try
                {
                    handler();
                }
                catch (Exception error)
                {
                    ObserverError = error;
                }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            epoch++;
            Current = null;
            try
            {
                cancellation.Cancel();
            }
            catch (Exception error)
            {
                ObserverError = error;
            }
            finally
            {
                cancellation.Dispose();
            }
            Notify();
            Changed = null;
        }
    }

    public sealed class JoinIntent
    {
        public string Provider { get; }
        public string Identity { get; }
        public string Payload { get; }

        public JoinIntent(string provider, string identity, string payload)
        {
            if (
                string.IsNullOrWhiteSpace(provider)
                || string.IsNullOrWhiteSpace(identity)
                || payload == null
                || payload.Length > 4096
            )
                throw new ArgumentException("Invalid join intent.");
            Provider = provider;
            Identity = identity;
            Payload = payload;
        }
    }

    public interface IJoinIntents
    {
        bool TryTake(out JoinIntent intent);
        int Pending { get; }
    }

    /// <summary>Bounded startup handoff. The SDK adapter captures; game readiness decides when to consume.</summary>
    public sealed class JoinIntentBuffer : IJoinIntents
    {
        private readonly Queue<JoinIntent> pending = new Queue<JoinIntent>();
        private readonly Queue<string> history = new Queue<string>();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly int capacity;
        public int Pending => pending.Count;
        public int Rejected { get; private set; }

        public JoinIntentBuffer(int capacity = 32)
        {
            if (capacity < 1 || capacity > 4096)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public bool Capture(JoinIntent intent)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));
            string key = intent.Provider.Length + ":" + intent.Provider + intent.Identity;
            if (seen.Contains(key))
                return false;
            if (pending.Count == capacity)
            {
                Rejected++;
                return false;
            }
            pending.Enqueue(intent);
            seen.Add(key);
            history.Enqueue(key);
            while (history.Count > capacity * 2)
                seen.Remove(history.Dequeue());
            Iris.TryNotify(new JoinIntentReceived(intent));
            return true;
        }

        public bool TryTake(out JoinIntent intent)
        {
            if (pending.Count == 0)
            {
                intent = null;
                return false;
            }
            intent = pending.Dequeue();
            return true;
        }
    }
}
