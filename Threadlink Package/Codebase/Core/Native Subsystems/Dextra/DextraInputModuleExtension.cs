namespace Threadlink.Core.Subsystems.Dextra.Extensions
{
	using Core;
	using Propagator;
	using UnityEngine;

	public abstract class DextraInputModuleExtension : LinkableAssetSingleton<DextraInputModuleExtension>
	{
		[SerializeField] private DextraAction cancelAction = new();
		[SerializeField] private DextraAction pauseAction = new();
		[SerializeField] private DextraAction interactAction = new();

		public override void Discard()
		{
			interactAction.Discard();
			pauseAction.Discard();
			cancelAction.Discard();

			cancelAction = null;
			pauseAction = null;
			interactAction = null;

			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();
			static void Interact() => Propagator.Publish<bool>(PropagatorEvents.OnInteract);
			static void PauseGame() => Propagator.Publish(PropagatorEvents.OnPauseInput);

			cancelAction.Handle(Dextra.Cancel);
			pauseAction.Handle(PauseGame);
			interactAction.Handle(Interact);
		}
	}
}
