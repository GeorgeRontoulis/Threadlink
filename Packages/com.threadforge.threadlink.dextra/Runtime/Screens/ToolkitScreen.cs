namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>UI Toolkit view adapter for an explicitly configured UIDocument and PanelSettings.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class ToolkitScreen : MonoBehaviour, IScreenView, IBackPolicy
    {
        [SerializeField]
        [Tooltip("Close this screen when the player presses Back (Project Settings ▸ Threadlink ▸ Dextra), unless something on it handles Back.")]
        private bool closesOnBack;

        /// <summary>Whether Back closes this screen; read when the screen opens.</summary>
        public bool ClosesOnBack
        {
            get => closesOnBack;
            set => closesOnBack = value;
        }

        private VisualElement root;
        private Focusable previous;

        /// <summary>Prepares the authored document while keeping its root hidden.</summary>
        public virtual UniTask PrepareAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            gameObject.SetActive(true);
            var document = GetComponent<UIDocument>();
            document.enabled = true;
            root = document.rootVisualElement;
            if (root == null)
                throw new System.InvalidOperationException("UIDocument has no prepared root.");
            root.style.display = DisplayStyle.None;
            root.focusable = true;
            return UniTask.CompletedTask;
        }

        /// <summary>Changes document-root presentation.</summary>
        public void SetVisible(bool visible)
        {
            if (root == null)
                return;
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            ScreenListeners.Visible(gameObject, visible);
        }

        /// <summary>Restores the previously focused element inside this document.</summary>
        public void SetFocused(bool focused)
        {
            if (root == null)
                return;
            ScreenListeners.Focused(gameObject, focused);
            if (focused)
                (previous ?? root).Focus();
            else
            {
                var element = root.focusController?.focusedElement;
                if (element is VisualElement visual && (visual == root || root.Contains(visual)))
                {
                    previous = element;
                    element.Blur();
                }
            }
        }

        /// <summary>Hides and releases focus; content ownership remains with the factory.</summary>
        public virtual void Dispose()
        {
            SetFocused(false);
            SetVisible(false);
        }
    }
}
