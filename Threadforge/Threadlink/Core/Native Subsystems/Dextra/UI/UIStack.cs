namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Addressables;
    using Initium;
    using Iris;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.Objects;

    /// <summary>
    /// A State-Machine-like object controlling how user interfaces stack. 
    /// </summary>
    public sealed class UIStack : IThreadlinkSingleton, IInitializable
    {
        internal int StackedInterfacesCount => StackedInterfaces.Count;

        private Stack<Type> StackedInterfaces { get; set; }
        private Dictionary<Type, UserInterface> CreatedInterfaces { get; set; }

        public void Discard()
        {
            StackedInterfaces.Clear();
            StackedInterfaces.TrimExcess();
            StackedInterfaces = null;

            var interfaces = CreatedInterfaces.Values;

            foreach (var ui in interfaces)
                ui.Discard();

            CreatedInterfaces.Clear();
            CreatedInterfaces.TrimExcess();
            CreatedInterfaces = null;
        }

        public void Boot()
        {
            StackedInterfaces = new(1);

            var createdInterfaces = CreatedInterfaces.Values.OfType<IBootable>();

            if (createdInterfaces != null)
            {
                foreach (var userInterface in createdInterfaces)
                    Initium.Boot(userInterface);
            }
        }

        public void Initialize()
        {
            var userInterfaces = CreatedInterfaces.Values;

            foreach (var userInterface in userInterfaces)
            {
                if (userInterface is IInitializable initializable)
                    Initium.Initialize(initializable);
            }
        }

        internal void CreateAllInterfaces(ReadOnlySpan<GroupedAssetPointer> pointers)
        {
            int length = pointers.Length;

            CreatedInterfaces = new(length);

            for (int i = 0; i < length; i++)
            {
                var pointer = pointers[i];

                if (Threadlink.TryGetPrefabKey(pointer.Group, pointer.IndexInDatabase, out var runtimeKey)
                && ThreadlinkResourceProvider<GameObject>.LoadOrGetCachedAt(runtimeKey).As<UserInterface>(out var loadedUIComponent))
                {
                    string originalName = loadedUIComponent.name;
                    var userInterface = UnityEngine.Object.Instantiate(loadedUIComponent);

                    userInterface.name = originalName;
                    UnityEngine.Object.DontDestroyOnLoad(userInterface.gameObject);

                    userInterface.ForceCanvasGroupAlphaTo(0);
                    userInterface.SetInteractableState(false);

                    CreatedInterfaces.Add(userInterface.GetType(), userInterface);
                }
            }
        }

        internal bool TryGetTopInterface(out UserInterface result)
        {
            if (StackedInterfaces.TryPeek(out var userInterfaceID) && CreatedInterfaces.TryGetValue(userInterfaceID, out result))
                return true;

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsTopInterface<T>() where T : UserInterface => TryGetTopInterface(out var top) && typeof(T).Equals(top.GetType());

        internal void ClearStack()
        {
            foreach (var stackedUI in StackedInterfaces)
            {
                if (CreatedInterfaces.TryGetValue(stackedUI, out var userInterface))
                {
                    userInterface.OnPopped();

                    if (userInterface is ICancellableInterface cancellableInterface)
                    {
                        cancellableInterface.OnCancelled();
                        Iris.Publish(Iris.Events.OnUICancelled, userInterface);
                    }
                }
            }

            StackedInterfaces.Clear();
        }

        internal void Cancel()
        {
            if (TryGetTopInterface(out var topUI) && topUI is ICancellableInterface cancellableInterface)
            {
                StackedInterfaces.Pop();
                topUI.OnPopped();

                if (TryGetTopInterface(out var newTopUI))
                    newTopUI.OnResurfaced();

                cancellableInterface.OnCancelled();
                Iris.Publish(Iris.Events.OnUICancelled, topUI);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Stack<T>() where T : UserInterface
        {
            var type = typeof(T);

            if (!IsTopInterface<T>()
            && CreatedInterfaces.TryGetValue(type, out var target))
            {
                if (TryGetTopInterface(out var topUI))
                    topUI.OnCovered();

                StackedInterfaces.Push(type);
                target.OnStacked();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Stack<U, D>(D stackingData) where U : UserInterface
        {
            var type = typeof(U);

            if (!IsTopInterface<U>() && CreatedInterfaces.TryGetValue(type, out var target))
            {
                if (TryGetTopInterface(out var topUI))
                    topUI.OnCovered();

                StackedInterfaces.Push(type);

                if (target is IStackingDataPreprocessor<D> preprocessor)
                    preprocessor.Preprocess(stackingData);

                target.OnStacked();
            }
        }
    }
}
