namespace Threadlink.Utilities.Events
{
	using System;
	using System.Linq;

	public static class EventOperations
	{
		public static int GetListenerCount(this Delegate source) { return source.GetInvocationList().Length; }

		public static bool Contains(this Delegate source, Delegate target)
		{
			if (source == null) return false;

			return source.GetInvocationList().Contains(target);
		}
	}
}