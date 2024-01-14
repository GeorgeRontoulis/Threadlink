namespace Threadlink.Utilities.UnityLogging
{
	using UnityEngine;

	public enum DebugNotificationType { Info, Warning, Error }

	public static class UnityConsole
	{
		private static string ToSingleString(params object[] objects)
		{
			return Text.String.Construct(objects);
		}

		public static void Notify(params object[] objects)
		{
			Debug.Log(ToSingleString(objects));
		}

		public static void Notify(DebugNotificationType notificationType, params object[] objects)
		{
			string notification = ToSingleString(objects);

			switch (notificationType)
			{
				default: Debug.Log(notification); break;

				case DebugNotificationType.Warning: Debug.LogWarning(notification); break;

				case DebugNotificationType.Error: Debug.LogError(notification); break;
			}
		}
	}
}