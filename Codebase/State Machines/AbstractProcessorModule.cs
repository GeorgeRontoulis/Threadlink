namespace Threadlink.StateMachines
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;

	internal abstract class AbstractProcessorModule : LinkableAsset
	{
		internal abstract VoidOutput Run(VoidInput input);
	}
}