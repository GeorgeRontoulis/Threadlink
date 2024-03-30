namespace Threadlink.Utilities.Events
{
	using System;
	using System.Linq;

	public static class EventUtilities
	{
		private static Delegate[] GetSubscribers(this Delegate source)
		{
			return source.GetInvocationList();
		}

		public static int GetListenerCount(this Delegate source)
		{
			return source.GetSubscribers().Length;
		}

		public static bool Contains(this Delegate source, Delegate target)
		{
			if (source == null) return false;

			return source.GetSubscribers().Contains(target);
		}

		public static bool Evaluate(this GenericEvent<bool> source)
		{
			if (source == null) return false;

			var subscribers = source.InvocationList;
			int length = subscribers.Length;

			for (int i = 0; i < length; i++)
				if ((subscribers[i] as ThreadlinkDelegate<bool, VoidInput>).Invoke(default) == false) return false;

			return true;
		}
	}
}