namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Iris;
    using Scribe;
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Layouts;
    using UnityEngine.UI;

    /// <summary>
    /// Attach this component to UI images indicating device-specific input actions.
    /// The image will be updated with the <see cref="Sprite"/> corresponding to the
    /// <see cref="InputActionReference"/>. The configuration for these
    /// icons lives in the <see cref="DextraConfig"/> asset and is loaded at runtime
    /// through the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class DextraInputIcon : MonoBehaviour
    {
        private const Iris.Events DEVICE_CHANGED_EVENT = Iris.Events.OnInputDeviceChanged;

        [HideInInspector, SerializeField] private Image targetImage = null;

        [SerializeField] private DextraInputControlPath inputControlPath = null;

        private void OnValidate()
        {
            var image = GetComponent<Image>();

            if (targetImage != image)
                targetImage = image;

            if (targetImage != null)
            {
                targetImage.type = Image.Type.Simple;
                targetImage.preserveAspect = true;
            }
        }

        private void OnDestroy()
        {
            ListenForInputDeviceChanges(false);
        }

        internal void ListenForInputDeviceChanges(bool listen)
        {
            if (listen)
            {
                OnInputDeviceChanged(Dextra.Instance.CurrentInputDevice);
                Iris.Subscribe<Action<Dextra.InputDevice>>(DEVICE_CHANGED_EVENT, OnInputDeviceChanged);
            }
            else Iris.Unsubscribe<Action<Dextra.InputDevice>>(DEVICE_CHANGED_EVENT, OnInputDeviceChanged);
        }

        private void OnInputDeviceChanged(Dextra.InputDevice inputDevice)
        {
            if (!string.IsNullOrEmpty(inputControlPath))
            {
                targetImage.enabled = Dextra.Instance.TryGetInputIcon(inputDevice, inputControlPath, out var icon);
                targetImage.sprite = icon;
            }
            else this.Send(nameof(inputControlPath), " is unassigned!").ToUnityConsole(DebugType.Warning);
        }
    }
}
