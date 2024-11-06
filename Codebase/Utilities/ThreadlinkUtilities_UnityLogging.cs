namespace Threadlink.Utilities.UnityLogging
{
	using UnityEngine;

	public enum DebugNotificationType { Info, Warning, Error }

	public static class UnityConsole
	{
		private static string ToSingleString(params object[] objects)
		{
			return Text.TLZString.Construct(objects);
		}

		public static void Notify(Object context = null, params object[] objects)
		{
			Debug.Log(ToSingleString(objects), context);
		}

		public static void Notify(DebugNotificationType notificationType = DebugNotificationType.Info, Object context = null, params object[] objects)
		{
			string notification = ToSingleString(objects);

			switch (notificationType)
			{
				default: Debug.Log(notification, context); break;

				case DebugNotificationType.Warning: Debug.LogWarning(notification, context); break;

				case DebugNotificationType.Error: Debug.LogError(notification, context); break;
			}
		}
	}
}