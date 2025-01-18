namespace Threadlink.StateMachines.Processors
{
	using Core.StorageAPI;

	/// <summary>
	/// Processors are attached to state machine and execute logic every frame.
	/// </summary>
	public interface IProcessor
	{
		public UpdateMode Mode { get; }

		public void Boot(ThreadlinkStorage paramStorage);
		public void Run(ThreadlinkStorage paramStorage);
	}
}