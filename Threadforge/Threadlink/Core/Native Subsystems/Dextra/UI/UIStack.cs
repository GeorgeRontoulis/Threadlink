namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Generated;
    using Initium;
    using Iris;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A State-Machine-like object controlling how user interfaces stack. 
    /// </summary>
    public sealed class UIStack : IThreadlinkSingleton, IInitializable
    {
        internal int StackedInterfacesCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => StackedInterfaces.Count;
        }

        private Stack<Type> StackedInterfaces { get; set; } = null;
        private Dictionary<Type, UserInterface> CreatedInterfaces { get; set; } = null;

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
            if (CreatedInterfaces == null)
                return;

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
            if (CreatedInterfaces == null)
                return;

            var userInterfaces = CreatedInterfaces.Values;

            foreach (var userInterface in userInterfaces)
            {
                if (userInterface is IInitializable initializable)
                    Initium.Initialize(initializable);
            }
        }

        internal void CreateAllInterfaces(ReadOnlySpan<ThreadlinkIDs.Addressables.Prefabs> pointers)
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return;

            int length = pointers.Length;

            CreatedInterfaces = new(length);

            UserInterface ui;

            for (int i = 0; i < length; i++)
            {
                ui = core.LoadPrefab<UserInterface>(pointers[i]);

                if (ui != null)
                {
                    string originalName = ui.name;

                    var userInterface = UnityEngine.Object.Instantiate(ui);

                    CreatedInterfaces[userInterface.GetType()] = userInterface;

                    userInterface.name = originalName;
                    UnityEngine.Object.DontDestroyOnLoad(userInterface.gameObject);

                    userInterface.ForceCanvasGroupAlphaTo(0f);
                    userInterface.SetInteractableState(false);
                }
            }
        }

        internal bool TryGetTopInterface(out UserInterface result)
        {
            if (StackedInterfaces.TryPeek(out var uiID) && CreatedInterfaces.TryGetValue(uiID, out result))
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
                    userInterface.OnPopped();
            }

            StackedInterfaces.Clear();
        }

        internal void PopTopInterface()
        {
            if (TryGetTopInterface(out var topUI))
            {
                StackedInterfaces.Pop();
                topUI.OnPopped();

                if (TryGetTopInterface(out var newTopUI))
                    newTopUI.OnResurfaced();
            }
        }

        internal void Cancel()
        {
            if (TryGetTopInterface(out var topUI) && topUI is ICancellableInterface cancellableInterface)
            {
                if (cancellableInterface.IsInSubPanel)
                {
                    cancellableInterface.OnSubPanelCancelled();
                    PlayCancelSound();
                    return;
                }

                StackedInterfaces.Pop();
                topUI.OnPopped();

                if (TryGetTopInterface(out var newTopUI))
                    newTopUI.OnResurfaced();

                cancellableInterface.OnCancelled();
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnUICancelled, topUI);
                PlayCancelSound();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Stack<T>() where T : UserInterface
        {
            var type = typeof(T);

            if (!IsTopInterface<T>() && CreatedInterfaces.TryGetValue(type, out var target))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PlayCancelSound()
        {
            if (Aura.Aura.TryGetSingleton(out var aura))
                aura.PlayUISFX(Aura.Aura.UISFX.Cancel);
        }
    }
}
