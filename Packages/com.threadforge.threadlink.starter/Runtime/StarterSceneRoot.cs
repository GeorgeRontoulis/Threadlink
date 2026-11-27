namespace Threadforge.Threadlink.Starter
{
    using System;
    using UnityEngine;

    /// <summary>A starter scene's root: where the camera looks while the scene is shown.</summary>
    public sealed class StarterSceneRoot : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("This scene's content objects, for scripts that need them.")]
        private GameObject[] content = Array.Empty<GameObject>();

        [SerializeField]
        [Tooltip("Transform the camera follows.")]
        private Transform cameraAnchor;

        [SerializeField]
        [Tooltip("Camera offset from the anchor.")]
        private Vector3 followOffset;

        /// <summary>The scene's content objects.</summary>
        public GameObject[] Content => content;

        private void OnEnable()
        {
            // Nexus activates the scene's roots when it presents the scene; the camera follows the presented scene.
            if (cameraAnchor != null)
                StarterCameraRig.Follow(cameraAnchor, followOffset);
        }
    }
}
