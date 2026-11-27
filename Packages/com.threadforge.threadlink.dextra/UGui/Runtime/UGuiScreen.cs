namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.EventSystems;

    /// <summary>Canvas-based view adapter. Content instance ownership remains with the selected factory.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UGuiScreen : MonoBehaviour, IScreenView, IBackPolicy
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

        [SerializeField]
        [Tooltip("Selected when the screen takes focus, for gamepad and keyboard navigation.")]
        private GameObject firstSelected;
        private GameObject previousSelected;
        private CanvasGroup group;

        /// <summary>Override for required screen preparation; called before the host commits presentation.</summary>
        public virtual UniTask PrepareAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        /// <summary>Changes rendering/raycast participation without destroying the retained screen.</summary>
        public void SetVisible(bool visible)
        {
            if (group == null)
                group = GetComponent<CanvasGroup>();
            group.alpha = visible ? 1 : 0;
            group.blocksRaycasts = visible;
            if (visible)
                gameObject.SetActive(true);
            ScreenListeners.Visible(gameObject, visible);
        }

        /// <summary>Restores a previously selected control when this screen regains focus.</summary>
        public void SetFocused(bool focused)
        {
            if (group == null)
                group = GetComponent<CanvasGroup>();
            group.interactable = focused;
            ScreenListeners.Focused(gameObject, focused);
            var events = EventSystem.current;
            if (events == null)
                return;
            if (!focused)
            {
                var selected = events.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(transform))
                {
                    previousSelected = selected;
                    events.SetSelectedGameObject(null);
                }
            }
            else
                events.SetSelectedGameObject(
                    previousSelected != null ? previousSelected : firstSelected
                );
        }

        /// <summary>Releases presentation state; the owning content request destroys the instance.</summary>
        public virtual void Dispose()
        {
            SetFocused(false);
            SetVisible(false);
        }
    }
}
