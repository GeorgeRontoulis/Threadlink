namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.Localization;

    /// <summary>A lantern to collect. Its identity is stable so saves remember it.</summary>
    public sealed class StarterCollectible : Interactable
    {
        [SerializeField]
        [Tooltip("Stable ID saves use to remember this item. Assigned automatically.")]
        private string identity;

        [SerializeField]
        [Tooltip("Item name: an entry of a string table. The prompt entry shows it as {item}.")]
        private LocalizedString displayName = new LocalizedString();

        [SerializeField]
        [Tooltip("Hidden once collected.")]
        private GameObject visual;

        private bool collected;

        /// <summary>Stable identity stored in saves.</summary>
        public string Identity => identity;

        /// <summary>The prompt entry, with this item's name as its <c>{item}</c> variable.</summary>
        public override LocalizedString Prompt
        {
            get
            {
                var prompt = base.Prompt;
                if (!prompt.ContainsKey("item"))
                    prompt.Add("item", displayName);
                return prompt;
            }
        }

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
