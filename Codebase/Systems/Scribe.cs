namespace Threadlink.Systems
{
	using Core;
	using System;
	using Utilities.UnityLogging;
	using String = Utilities.Text.String;

	/// <summary>
	/// System responsible for logging all Threadlink-specific operations.
	/// </summary>
	public sealed class Scribe : LinkableBehaviourSingleton<Scribe>
	{
		public const DebugNotificationType InfoNotif = DebugNotificationType.Info;
		public const DebugNotificationType WarningNotif = DebugNotificationType.Warning;
		public const DebugNotificationType ErrorNotif = DebugNotificationType.Error;

#if UNITY_EDITOR && THREADLINK_SCRIBE
		[UnityEngine.SerializeField] private bool pauseOnSystemLog = false;
#endif

		public override void Boot() { Instance = this; }
		public override void Initialize() { }

		public static void SystemLog(string systemID, DebugNotificationType logType, params string[] message)
		{
#if THREADLINK_SCRIBE
			string temp = String.Construct(message);
			string systemMessage = String.Construct("[", systemID, "] - ", temp);

			switch (logType)
			{
				case InfoNotif:
				LogInfo(systemMessage);
				break;
				case WarningNotif:
				LogWarning(systemMessage);
				break;
				case ErrorNotif:
				LogError<ApplicationException>(systemMessage);
				break;
			}

#if UNITY_EDITOR
			if (Instance != null && Instance.pauseOnSystemLog) UnityEditor.EditorApplication.isPaused = true;
#endif
#endif
		}

		/// <summary>
		/// Exception overload of the <see cref="SystemLog(string, DebugNotificationType, string[])"/> method. Throws the exception when called.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="systemID"></param>
		/// <param name="message"></param>
		public static void SystemLog<T>(string systemID, params string[] message)
		where T : Exception, new()
		{
			//We are not restricting this piece of code to the #THREADLINK_SCRIBE
			//define symbol to keep the throwing functionality for reusability.
			string temp = String.Construct(message);
			string systemMessage = String.Construct("[", systemID, "] - ", temp);

			LogError<T>(systemMessage);
#if UNITY_EDITOR
			if (Instance != null && Instance.pauseOnSystemLog) UnityEditor.EditorApplication.isPaused = true;
#endif
		}

		public static void LogInfo(params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(message);
#endif
		}

		public static void LogWarning(params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(WarningNotif, message);
#endif
		}

		public static void LogError<T>(string exceptionMessage) where T : Exception, new()
		{
			//We are not restricting this piece of code to the #THREADLINK_SCRIBE
			//define symbol to keep the throwing functionality for reusability.
			throw (T)Activator.CreateInstance(typeof(T), exceptionMessage);
		}
	}
}