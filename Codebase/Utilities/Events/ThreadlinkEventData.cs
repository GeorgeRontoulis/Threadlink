namespace Threadlink.Utilities.Events
{
	public delegate Output ThreadlinkDelegate<Output, Input>(Input input);

	public struct VoidInput { }
	public struct VoidOutput { }
}
