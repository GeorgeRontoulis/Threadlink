namespace Threadlink.StateMachines.Processors
{
	using Core.Subsystems.Vault;
	using Core;

	/// <summary>
	/// Processors are attached to state machines and execute logic in intervals
	/// according to the specified <see cref="Mode"/>.
	/// </summary>
	public interface IProcessor : IDiscardable
	{
		public UpdateMode Mode { get; }

		public void Boot(Vault parameters);
		public void Run(Vault parameters);
	}
}