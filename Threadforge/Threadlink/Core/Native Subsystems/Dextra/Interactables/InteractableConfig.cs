namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using UnityEngine;
    using UnityEngine.Localization;
    using Utilities.Localization;

    public enum InteractionOptions : byte
    {
        None = 0,
        InteractOnContact = 1 << 0,
    }

    /// <summary>
    /// You may extend this class for your own interactables.
    /// </summary>
    [CreateAssetMenu(menuName = "Threadlink/Dextra/Interactable Config")]
    public class InteractableConfig : ScriptableObject
    {
        protected internal InteractionOptions InteractionOptions => interactionOptions;

        /// <summary>
        /// Synchronously retrieves the localized prompt.
        /// </summary>
        protected internal string InteractionPrompt => interactionPrompt.GetSafeLocalizedString();

        [SerializeField] private InteractionOptions interactionOptions = 0;
        [SerializeField] private LocalizedString interactionPrompt = new();
    }
}