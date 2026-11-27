namespace Threadforge.Threadlink.Starter
{
    using UnityEngine;

    /// <summary>Moves with the Gameplay/Move action of player 0. Movement stops while a screen blocks gameplay input.</summary>
    public sealed class StarterPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Movement speed in meters per second.")]
        private float speed = 5;

        [SerializeField]
        [Tooltip("Turn speed in degrees per second.")]
        private float turnSpeed = 720;

        /// <summary>Places the player without moving through the world.</summary>
        public void Teleport(Vector3 position)
        {
            if (TryGetComponent<CharacterController>(out var controller))
            {
                controller.enabled = false;
                transform.position = position;
                controller.enabled = true;
            }
            else
                transform.position = position;
        }

        private void Update()
        {
            if (!Dextra.TryGetSingleton(out _) || Dextra.Players.Count == 0)
                return;
            var input = Dextra.Player().Read(StarterInput.Gameplay.Move);
            var move = new Vector3(input.x, 0, input.y);
            if (move.sqrMagnitude < 0.0001f)
                return;
            move = Vector3.ClampMagnitude(move, 1);
            if (TryGetComponent<CharacterController>(out var controller))
                controller.Move(move * (speed * Time.deltaTime));
            else
                transform.position += move * (speed * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(move, Vector3.up), turnSpeed * Time.deltaTime);
        }
    }
}
