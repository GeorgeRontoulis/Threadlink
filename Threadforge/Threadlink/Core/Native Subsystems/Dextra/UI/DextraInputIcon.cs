namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Iris;
    using Scribe;
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;

    /// <summary>
    /// Attach this component to UI images indicating device-specific input actions.
    /// The image will be updated with the <see cref="Sprite"/> corresponding to the 
    /// <see cref="InputActionReference.m_ActionId"/>. The configuration for these
    /// icons lives in the <see cref="DextraConfig"/> asset, is serialized into binary
    /// and loaded at runtime through the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
    /// See <see cref="DextraConfig.inputIconsAuthoringTable"/> for more insight into how the binary file is compiled.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class DextraInputIcon : MonoBehaviour
    {
        private const Iris.Events DEVICE_CHANGED_EVENT = Iris.Events.OnInputDeviceChanged;

        [HideInInspector, SerializeField] private Image targetImage = null;
        [SerializeField] private InputActionReference targetAction = null;

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

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                OnInputDeviceChanged(Dextra.Instance.CurrentInputDevice);
                Iris.Subscribe<Action<Dextra.InputDevice>>(DEVICE_CHANGED_EVENT, OnInputDeviceChanged);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                Iris.Unsubscribe<Action<Dextra.InputDevice>>(DEVICE_CHANGED_EVENT, OnInputDeviceChanged);
        }

        private void OnInputDeviceChanged(Dextra.InputDevice inputDevice)
        {
            var action = targetAction.action;

            if (action != null)
            {
                targetImage.enabled = Dextra.Instance.TryGetInputIcon(inputDevice, action.id, out var icon);
                targetImage.sprite = icon;
            }
            else this.Send(nameof(targetAction), " is unassigned!").ToUnityConsole(DebugType.Warning);
        }
    }
}
