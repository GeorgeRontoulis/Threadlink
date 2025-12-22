namespace Threadlink.Core
{
    using Shared;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Base class allowing for the creation of a Subsystem which can be
    /// connected to <see cref="Threadlink"/> through <see cref="Threadlink.Weave{T}"/>.
    /// <para></para>
    /// Subsystems make use of the <see href="https://blog.stephencleary.com/2022/09/modern-csharp-techniques-1-curiously-recurring-generic-pattern.html">Curiously Recurring Generic Pattern</see>
    /// to enforce type safety for their exposed static singletons.
    /// </summary>
    /// <typeparam name="Singleton">The singleton type of the subsystem.</typeparam>
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
    /// <summary>
    /// A specialized type of subsystem acting as a register of objects.
    /// Internally, it uses a <see cref="Dictionary{int, Object}"/> to keep
    /// track of its registered objects.
    /// <para></para>
    /// Objects that can be registered must at the very least derive from <see cref="IIdentifiable"/>,
    /// since their <see cref="IIdentifiable.ID"/> properties are used as the keys in the dictionary.
    /// <para></para>
    /// <see cref="Register{Singleton, Object}"/> and its subclasses is part of 
    /// <see cref="Threadlink"/>'s effort to reduce user boilerplate by providing 
    /// smart solutions commonly used by games and apps.
    /// <para></para>
    /// Use this as your base class if you want to build upon it with custom functionality.
    /// More specialized registers like <see cref="Linker{Singleton, Object}"/> and
    /// <see cref="Weaver{Singleton, Object}"/> offer further reduction of boilerplate as they
    /// cover the linking (discovery of existing objects in scene/memory) and weaving (creation and lifecycle
    /// management of new objects) respectively.
    /// </summary>
    /// <typeparam name="Singleton">The singleton type of the subsystem.</typeparam>
    /// <typeparam name="Object">The type of object that can be registered.</typeparam>
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
        public bool TryGetLinkedObject<T>(int linkID, out T result) where T : Object
        {
            if (Registry.TryGetValue(linkID, out var linkedObject))
            {
                result = (T)linkedObject;
                return true;
            }

            result = default;
            return false;
        }
    }
    #endregion

    #region Linking (Existing Object Detection for work, No Lifecycle Management):
    /// <summary>
    /// A specialized type of <see cref="Register{Singleton, Object}"/> that links
    /// existing objects to itself. It does not manage the lifecycle of its linked
    /// objects. It only links or disconnects existing objects using
    /// <see cref="TryLink(Object)"/> or <see cref="TryDisconnect{T}(int, out T)"/>
    /// respectively. 
    /// <para></para>
    /// See <see cref="Weaver{Singleton, Object}"/> if you need a way
    /// to create and manage the lifecycle of objects within the framework.
    /// </summary>
    /// <typeparam name="Singleton">The singleton type of the subsystem.</typeparam>
    /// <typeparam name="Object">The type of object that can be linked.</typeparam>
    public abstract class Linker<Singleton, Object> : Register<Singleton, Object>
    where Singleton : Linker<Singleton, Object>
    where Object : IIdentifiable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryLink(Object target) => Registry.TryAdd(target.ID, target);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryDisconnect<T>(int linkID, out T disconnectedObject) where T : Object
        {
            if (Registry.Remove(linkID, out var removedObject))
            {
                disconnectedObject = (T)removedObject;
                return true;
            }

            disconnectedObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void DisconnectAll(bool trimRegistry) => ClearRegistry(trimRegistry);
    }
    #endregion

    #region Weaving (New Object Creation for work, includes Lifecycle Management):
    /// <summary>
    /// A specialized type of <see cref="Register{Singleton, Object}"/> that creates and manages the lifecycle of objects using the 
    /// <see href="https://www.dofactory.com/net/factory-method-design-pattern">Factory Pattern.</see>
    /// <para></para>
    /// <see cref="TryWeave{T}(out T)"/> and <see cref="TryWeave{T}(T, out T)"/> create objects using <see cref="WeavingFactory{Object}"/>,
    /// while <see cref="TrySever(int)"/> discards the objects by calling <see cref="IDiscardable.Discard"/>.
    /// <para></para>
    /// Weavable objects must at the very least implement <see cref="IDiscardable"/>, since <see cref="IDiscardable.Discard"/>
    /// is used to dispose of them. Internally, this disposal calls Unity's <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/>
    /// when the weavable object derives either from <see cref="LinkableBehaviour"/> or <see cref="LinkableAsset"/>, unless overridden.
    /// <para></para>
    /// Fun fact: <see cref="Threadlink"/>, the core of the framework, is a 
    /// <see cref="Weaver{Singleton, Object}"/> weaving <see cref="IThreadlinkSubsystem"/>s!
    /// Even the core is a subsystem, and benefits from the boiletplate reduction <see cref="Weaver{Singleton, Object}"/> offers!
    /// </summary>
    /// <typeparam name="Singleton">The singleton type of the subsystem.</typeparam>
    /// <typeparam name="Object">The type of object that can be woven.</typeparam>
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

        public virtual bool TryWeave<T>(out T wovenObject) where T : Object
        {
            if (WeavingFactory<T>.TryCreate(out wovenObject))
            {
                Registry.Add(wovenObject.ID, wovenObject);
                return true;
            }

            return false;
        }

        public virtual bool TryWeave<T>(T original, out T wovenObject) where T : Object
        {
            if (WeavingFactory<T>.TryCreateFrom(original, out wovenObject))
            {
                Registry.Add(wovenObject.ID, wovenObject);
                return true;
            }

            return false;
        }
    }
    #endregion
}
