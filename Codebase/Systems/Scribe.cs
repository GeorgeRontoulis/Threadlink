namespace Threadlink.Systems
{
	using Core;
	using Cysharp.Text;
	using System;
	using System.Collections.Generic;
	using Utilities.Collections;
	using Utilities.UnityLogging;

	/// <summary>
	/// System responsible for logging all Threadlink-specific operations.
	/// </summary>
	public static class Scribe
	{
		private static readonly Dictionary<Type, Exception> ExceptionPool = new(1);

		public const DebugNotificationType InfoNotif = DebugNotificationType.Info;
		public const DebugNotificationType WarningNotif = DebugNotificationType.Warning;
		public const DebugNotificationType ErrorNotif = DebugNotificationType.Error;

		public static void ResetExceptionPool()
		{
			ExceptionPool.Clear();
			ExceptionPool.TrimExcess();
		}

		private static string ConstructSystemMessage(string systemID, params object[] message)
		{
			using var sb = ZString.CreateUtf8StringBuilder(true);

			sb.Append("[");
			sb.Append(systemID);
			sb.Append("] - ");

			int length = message.Length;
			for (int i = 0; i < length; i++) sb.Append(message[i]);

			return sb.ToString();
		}

		public static void LogInfo(this UnityEngine.Object source, params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(source, message);
#endif
		}

		public static void LogWarning(this UnityEngine.Object source, params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(WarningNotif, source, message);
#endif
		}

		public static void SystemLog(this ThreadlinkSystem source, DebugNotificationType type = InfoNotif, params string[] message)
		{
#if THREADLINK_SCRIBE
			if (type.Equals(ErrorNotif)) type = WarningNotif;
			UnityConsole.Notify(type, source, ConstructSystemMessage(source.LinkID, message));
#endif
		}

		public static T SystemLog<T>(this ThreadlinkSystem source, bool throwException = true) where T : Exception, new()
		{
			return LogException<T>(source, throwException);
		}

		public static T LogException<T>(this IIdentifiable source, bool throwException = true) where T : Exception, new()
		{
			if (ExceptionPool.TryGetValue(typeof(T), out Exception exception) == false)
			{
				exception = new T();
				ExceptionPool.Add(typeof(T), exception);
			}

			var ctx = source is UnityEngine.Object ? source as UnityEngine.Object : null;

			if (throwException)
			{
				LogWarning(ctx, ConstructSystemMessage(typeof(Threadlink).Name, "Error Detected! Exception thrown below!"));
				throw exception;
			}
#if THREADLINK_SCRIBE
			else
			{
				string message = source is IThreadlinkSystem ? ConstructSystemMessage(source.LinkID, exception.Message) : exception.Message;
				UnityConsole.Notify(ErrorNotif, ctx, message);
				return exception as T;
			}
#endif
		}
	}
}