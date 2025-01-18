namespace Threadlink.Core.Subsystems.Dextra
{
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Dextra/Interactable Config")]
	public sealed class InteractableConfig : ScriptableObject
	{
		public Interactable.InteractionOptions InteractionOptions => interactionOptions;
		public string InteractionPrompt => interactionPrompt;

		[SerializeField] private Interactable.InteractionOptions interactionOptions = 0;
		[SerializeField] private string interactionPrompt = "Interact";
	}
}