namespace Threadforge.Threadlink.Starter
{
    using UnityEngine;

    /// <summary>Moves the bootstrap scene's single camera and listener to the presented scene's anchor.</summary>
    [DisallowMultipleComponent]
    public sealed class StarterCameraRig : MonoBehaviour
    {
        private static StarterCameraRig instance;
        private Transform target;
        private Vector3 offset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Only one StarterCameraRig is used; disabling the duplicate.", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>Follows an anchor until another scene is presented. A non-zero offset keeps a world-space
        /// distance and looks at the anchor; zero copies the anchor's pose.</summary>
        public static void Follow(Transform anchor, Vector3 offset = default)
        {
            if (instance == null)
                return;
            instance.target = anchor;
            instance.offset = offset;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            if (offset == Vector3.zero)
                transform.SetPositionAndRotation(target.position, target.rotation);
            else
                transform.SetPositionAndRotation(target.position + offset, Quaternion.LookRotation(-offset, Vector3.up));
        }
    }
}
