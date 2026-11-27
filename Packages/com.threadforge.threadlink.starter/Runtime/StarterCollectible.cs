namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;

    /// <summary>A lantern to collect. Its identity is stable so saves remember it.</summary>
    public sealed class StarterCollectible : Interactable
    {
        [SerializeField]
        [Tooltip("Stable ID saves use to remember this item. Assigned automatically.")]
        private string identity;

        [SerializeField]
        [Tooltip("Name shown when picked up.")]
        private string displayName = "Lantern";

        [SerializeField]
        [Tooltip("Hidden once collected.")]
        private GameObject visual;

        private bool collected;

        /// <summary>Stable identity stored in saves.</summary>
        public string Identity => identity;

        /// <inheritdoc />
        public override string Prompt => "Take " + displayName;

        /// <inheritdoc />
        public override bool CanInteract(GameObject actor) => base.CanInteract(actor) && !collected;

        /// <summary>Shows or hides the item.</summary>
        internal void SetCollected(bool value)
        {
            collected = value;
            if (visual != null)
                visual.SetActive(!value);
        }

        /// <inheritdoc />
        protected override UniTask InteractAsync(GameObject actor, CancellationToken token)
        {
            SetCollected(true);
            StarterGame.Instance.Collected(this);
            return UniTask.CompletedTask;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(identity))
                identity = System.Guid.NewGuid().ToString("D");
        }
    }
}
