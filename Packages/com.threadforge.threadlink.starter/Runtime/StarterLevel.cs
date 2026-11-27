namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// The gameplay level in a scene: restores the save into the scene, routes the Interact action to the player's
    /// interaction sensor, and reports what the HUD shows. Subsystems are ready in Start; nothing waits.
    /// </summary>
    public sealed class StarterLevel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The player in this level.")]
        private StarterPlayer player;

        [SerializeField]
        [Tooltip("How close, in meters, the player must be to interact.")]
        private float interactionRadius = 2.5f;

        private StarterCollectible[] collectibles;
        private InteractionSensor sensor;
        private InputAction interact;

        /// <summary>The player.</summary>
        public StarterPlayer Player => player;

        /// <summary>The prompt of the selected interactable, or empty.</summary>
        public string Prompt => sensor != null && sensor.Target != null ? sensor.Prompt : string.Empty;

        /// <summary>Whether an interaction is running; autosave waits for it.</summary>
        public bool Busy => sensor != null && sensor.IsInteracting;

        private void Start()
        {
            collectibles = GetComponentsInChildren<StarterCollectible>(true);
            sensor = player.GetComponent<InteractionSensor>() ?? player.gameObject.AddComponent<InteractionSensor>();
            interact = Dextra.Player().Action(StarterInput.Gameplay.Interact);
            interact.performed += Interact;
            Iris.OnUpdate(this, deltaTime => StarterGame.Instance.Tick(deltaTime));
            if (StarterGame.TryGetSingleton(out var game) && StarterGame.Content != null)
                game.Attach(this);
        }

        private void OnDestroy()
        {
            if (interact != null)
                interact.performed -= Interact;
            if (StarterGame.TryGetSingleton(out var game))
                game.Detach(this);
        }

        private void Interact(InputAction.CallbackContext context) => sensor.InteractAsync().Forget();

        /// <summary>Applies a save: the player's position and which items are already collected.</summary>
        internal void Restore(StarterSave save, PreparedLevel level)
        {
            var position = save.Position is { Length: 3 } saved ? new Vector3(saved[0], saved[1], saved[2]) : level.Spawn;
            player.Teleport(position);
            foreach (var item in collectibles)
                item.SetCollected(save.Collected.Contains(item.Identity));
        }

        /// <summary>Writes the scene's state into the save.</summary>
        internal void Capture(StarterSave save)
        {
            var position = player.transform.position;
            save.Position = new[] { position.x, position.y, position.z };
        }

        private void OnValidate() => interactionRadius = Mathf.Max(0.5f, interactionRadius);
    }
}
