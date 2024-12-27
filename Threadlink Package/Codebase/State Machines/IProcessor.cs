namespace Threadlink.StateMachines.Processors
{
	using Core;
	using Core.StorageAPI;

	/// <summary>
	/// Processors are attached to state machine and execute logic every frame.
	/// </summary>
	public interface IProcessor : IBootable
	{
		public enum UpdateMode { Update, FixedUpdate, LateUpdate }

		public UpdateMode Mode { get; }

		public void Run(ref ThreadlinkStorage paramStorage);
	}
}