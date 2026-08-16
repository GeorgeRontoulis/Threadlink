namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Cysharp.Threading.Tasks;
    using Generated;
    using Iris;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.InputSystem.UI;

    /// <summary>
    /// An interface that can be cancelled by the player, enabling them to navigate to the previous screen.
    /// </summary>
    public interface ICancellableInterface
    {
        public bool IsInSubPanel { get; }

        /// <summary>
        /// Called when the interface is cancelled, i.e. closed.
        /// </summary>
        public void OnCancelled();

        /// <summary>
        /// Called when the interface exits one of its sub-panels, without closing completely.
        /// The interface itself is responsible for managing the state of its sub-panels.
        /// </summary>
        public void OnSubPanelCancelled();
    }

    /// <summary>
    /// An interface that doesn't get hidden when another one is stacked on top of it.
    /// </summary>
    public interface IPersistentInterface { }

    /// <summary>
    /// An interface containing interactable elements.
    /// </summary>
    public interface IInteractableInterface { }

    /// <summary>
    /// An interface containing interactable elements.
    /// This one allows you to also define the type of buttons it contains.
    /// </summary>
    public interface IInteractableInterface<T> : IInteractableInterface where T : DextraSelectable
    {
        public List<T> Selectables { get; }
        public T LastSelectable { get; }
    }

    /// <summary>
    /// An interface that can preprocess its own custom stacking data before being stacked.
    /// </summary>
    /// <typeparam name="T">The stacking data type.</typeparam>
    public interface IStackingDataPreprocessor<T> { public void Preprocess(T data); }

    public partial class Dextra
    {
        private UIStack UIStack { get; set; }

        public bool TopInterfaceIsInteractable
        {
            get => UIStack.TryGetTopInterface(out var ui) && ui is IInteractableInterface;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTopInterface<T>(out Dextra dextra) where T : UserInterface
        {
            return TryGetSingleton(out dextra) && dextra.UIStack.IsTopInterface<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTopInterface<T>() where T : UserInterface
        {
            return TryGetSingleton(out var dextra) && dextra.UIStack.IsTopInterface<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUIInputModuleActive(bool active)
        {
            if (UnityEventSystem != null && UnityEventSystem.TryGetComponent(out InputSystemUIInputModule module))
            {
                if (active)
                    module.ActivateModule();
                else
                    module.DeactivateModule();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEventSystemActive(bool active)
        {
            if (UnityEventSystem != null)
                UnityEventSystem.enabled = active;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cancel()
        {
            if (UIStack.StackedInterfacesCount > 1)
                ClearEventSystemSelection();

            UIStack.Cancel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PopTopInterface()
        {
            ClearEventSystemSelection();
            UIStack.PopTopInterface();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearStackedInterfaces()
        {
            ClearEventSystemSelection();
            UIStack.ClearStack();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTopInterface<T>(T userInterface) where T : UserInterface => UIStack.IsTopInterface<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stack<T>() where T : UserInterface => UIStack.Stack<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stack<U, D>(D data) where U : UserInterface => UIStack.Stack<U, D>(data);

        public void ClearEventSystemSelection()
        {
            UnityEventSystem.SetSelectedGameObject(null);
            OnUIElementSelected(null);
        }

        /// <summary>
        /// Instruct Unity's <see cref="UnityEngine.EventSystems.EventSystem"/> to select a new UI element.
        /// Internally invokes <see cref="Iris.Events.OnUIElementSelected"/> 
        /// as <see cref="System.Action{T}"/> where T : <see cref="GameObject"/>.
        /// </summary>
        /// <param name="element">The desired element.</param>
        /// <returns>A fire-and-forget task. 
        /// Call <see cref="UniTaskVoid.Forget"/> on the returned task.
        /// </returns>.
        public async UniTaskVoid SelectUIElement(GameObject element)
        {
            if (element == null)
            {
                ClearEventSystemSelection();
                return;
            }
            else if (element.Equals(UnityEventSystem.currentSelectedGameObject))
                return; // Already selected — native OnSelect already handled (or will handle) notification.

            #region Workaround against a Unity bug where the Event System misbehaves when changing selections.
            await Threadlink.WaitForFramesAsync(1);
            ClearEventSystemSelection();
            UnityEventSystem.gameObject.SetActive(false);
            await Threadlink.WaitForFramesAsync(1);
            UnityEventSystem.gameObject.SetActive(true);
            UnityEventSystem.SetSelectedGameObject(element);
            #endregion
        }

        internal async UniTaskVoid HandleNativeSelection(GameObject element)
        {
            if (UIStack.TryGetTopInterface(out var ui) && ui is IInteractableInterface)
                OnUIElementSelected(element);
            else
            {
                await Threadlink.WaitForFramesAsync(1);
                ClearEventSystemSelection();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnUIElementSelected(GameObject element) => Iris.Publish(ThreadlinkIDs.Iris.Events.OnUIElementSelected, element);
    }
}
