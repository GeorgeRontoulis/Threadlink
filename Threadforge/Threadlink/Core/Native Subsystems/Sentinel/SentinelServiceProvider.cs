namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Shared;
    using System;
    using System.Collections.Generic;

    public interface ISentinelService : IDiscardable { }

    public interface ISentinelServiceProvider
    {
        bool TryGetService<T>(out T service) where T : class, ISentinelService;
    }

    public abstract class SentinelServiceProvider : ISentinelServiceProvider, IDiscardable
    {
        private Dictionary<Type, ISentinelService> Services { get; set; } = new(4);

        protected bool RegisterService<T>(T service) where T : class, ISentinelService
        {
            if (service == null)
                return false;

            return Services.TryAdd(typeof(T), service);
        }

        protected bool ReplaceService<T>(T service) where T : class, ISentinelService
        {
            if (service == null)
                return false;

            if (Services.Remove(typeof(T), out var previous))
                previous?.Discard();

            Services[typeof(T)] = service;
            return true;
        }

        protected bool UnregisterService<T>(out T service) where T : class, ISentinelService
        {
            if (Services.Remove(typeof(T), out var removed) && removed is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public bool TryGetService<T>(out T service) where T : class, ISentinelService
        {
            if (Services != null &&
                Services.TryGetValue(typeof(T), out var candidate) &&
                candidate is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public virtual void Discard()
        {
            if (Services == null)
                return;

            foreach (var service in Services.Values)
                service?.Discard();

            Services.Clear();
            Services = null;
        }
    }
}
