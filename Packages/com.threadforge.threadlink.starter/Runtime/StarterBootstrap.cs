namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// The first scene's entry point. Threadlink has already deployed every subsystem; this only hands the starter its
    /// designer data and shows the menu.
    /// </summary>
    public sealed class StarterBootstrap : MonoBehaviour
    {
        [Tooltip("The starter's screens, sounds, first level and tuning.")]
        [SerializeField]
        private StarterContent content;

        private void Start() => StarterGame.StartAsync(content).Forget();
    }
}
