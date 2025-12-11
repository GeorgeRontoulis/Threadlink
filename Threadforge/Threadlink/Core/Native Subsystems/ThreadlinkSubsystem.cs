namespace Threadlink.Core
{
    using Shared;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public abstract class ThreadlinkSubsystem<Singleton> : IThreadlinkSubsystem<Singleton>
    where Singleton : ThreadlinkSubsystem<Singleton>
    {
        public static Singleton Instance { get; protected set; }

        public virtual int ID => GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Boot() => Instance = this as Singleton;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Discard()
        {
            Instance = null;
        }
    }

    #region Register:
    public abstract class Register<Singleton, Object> : ThreadlinkSubsystem<Singleton>
    where Singleton : Register<Singleton, Object>
    where Object : IIdentifiable
    {
        protected Dictionary<int, Object> Registry { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearRegistry(bool trimRegistry = false)
        {
            Registry.Clear();

            if (trimRegistry)
                Registry.TrimExcess();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            ClearRegistry(true);
            Registry = null;
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            Registry = new(1);
            base.Boot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasLinked(int linkID) => Registry.ContainsKey(linkID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLinkedObject(int linkID, out Object result) => Registry.TryGetValue(linkID, out result);
    }
    #endregion

    #region Linking (Existing Object Detection for work, No Lifecycle Management):
    public abstract class Linker<Singleton, Object> : Register<Singleton, Object>
    where Singleton : Linker<Singleton, Object>
    where Object : IIdentifiable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryLink(Object target) => Registry.TryAdd(target.ID, target);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryDisconnect(int linkID, out Object disconnectedObject) => Registry.Remove(linkID, out disconnectedObject);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void DisconnectAll(bool trimRegistry) => ClearRegistry(trimRegistry);
    }
    #endregion

    #region Weaving (New Object Creation for work, includes Lifecycle Management):
    public abstract class Weaver<Singleton, Object> : Register<Singleton, Object>
    where Singleton : Weaver<Singleton, Object>
    where Object : IDiscardable, IIdentifiable
    {
        public virtual bool TrySever(int linkID)
        {
            if (Registry.Remove(linkID, out var removedObject))
            {
                removedObject?.Discard();
                return true;
            }

            return false;
        }

        public virtual void SeverAll()
        {
            foreach (var id in Registry.Keys)
                Registry[id]?.Discard();

            ClearRegistry();
        }

        public virtual bool TryWeave<S>(out S wovenObject) where S : Object
        {
            if (WeavingFactory<S>.TryCreate(out wovenObject))
            {
                Registry.Add(wovenObject.ID, wovenObject);
                return true;
            }

            return false;
        }

        public virtual bool TryWeave<S>(S original, out S wovenObject) where S : Object
        {
            if (WeavingFactory<S>.TryCreateFrom(original, out wovenObject))
            {
                Registry.Add(wovenObject.ID, wovenObject);
                return true;
            }

            return false;
        }
    }
    #endregion
}
