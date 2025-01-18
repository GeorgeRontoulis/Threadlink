namespace Threadlink.Core.Subsystems.Dextra
{
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Dextra/Interactable Config")]
	public sealed class InteractableConfig : ScriptableObject
	{
		public InteractionOptions InteractionOptions => interactionOptions;
		public string InteractionPrompt => interactionPrompt;

		[SerializeField] private InteractionOptions interactionOptions = 0;
		[SerializeField] private string interactionPrompt = "Interact";
	}
}