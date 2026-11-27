namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;

    /// <summary>The level exit: completes the level once every lantern is collected.</summary>
    public sealed class StarterExit : Interactable
    {
        [SerializeField]
        [Tooltip("Stable ID of this exit. Assigned automatically.")]
        private string identity;

        /// <inheritdoc />
        public override string Prompt => "Leave the forest";

        /// <inheritdoc />
        protected override UniTask InteractAsync(GameObject actor, CancellationToken token) => StarterGame.Instance.ExitReachedAsync();

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(identity))
                identity = System.Guid.NewGuid().ToString("D");
        }
    }
}
