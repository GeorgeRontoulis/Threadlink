namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>Optional thread-safe ownership of disposable resources, released in reverse admission order.</summary>
    /// <remarks>Does not control activation, Unity callbacks, or object lifetime. Dispose closes admission
    /// before invoking cleanup. Cleanup runs outside the lock and attempts every resource even after failures.</remarks>
    public sealed class ResourceOwner : IDisposable
    {
        private readonly object gate = new object();
        private List<IDisposable> resources = new List<IDisposable>();

        /// <summary>Whether this owner has closed admission.</summary>
        public bool IsDisposed
        {
            get
            {
                lock (gate)
                    return resources == null;
            }
        }

        /// <summary>Transfers cleanup responsibility to this owner. Each admission represents one cleanup obligation.</summary>
        /// <remarks>If admission is closed, disposes the supplied resource immediately and throws.
        /// Do not admit the same non-idempotent resource more than once.</remarks>
        public T Own<T>(T resource)
            where T : IDisposable
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            lock (gate)
            {
                if (resources != null)
                {
                    resources.Add(resource);
                    return resource;
                }
            }
            resource.Dispose();
            throw new ObjectDisposedException(nameof(ResourceOwner));
        }

        /// <summary>Closes admission and releases all resources. Aggregates cleanup failures after attempting every release.</summary>
        /// <remarks>Repeated or reentrant calls are harmless. Concurrent callers do not wait for another caller's cleanup.</remarks>
        public void Dispose()
        {
            List<IDisposable> pending;
            lock (gate)
            {
                pending = resources;
                resources = null;
            }
            if (pending == null)
                return;
            List<Exception> errors = null;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                try
                {
                    pending[i].Dispose();
                }
                catch (Exception error)
                {
                    if (errors == null)
                        errors = new List<Exception>();
                    errors.Add(error);
                }
            }
            pending.Clear();
            if (errors != null)
                throw new AggregateException(
                    "One or more owned resources failed to dispose.",
                    errors
                );
        }
    }
}
