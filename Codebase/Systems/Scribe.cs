namespace Threadlink.Systems
{
	using System;
	using Threadlink.Core;
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

		public override void Boot() { }
		public override void Initialize() { Instance = this; }

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
				LogError(systemMessage);
				break;
			}

#if UNITY_EDITOR
			if (Instance != null && Instance.pauseOnSystemLog) UnityEditor.EditorApplication.isPaused = true;
#endif
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

		public static void LogError(params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(ErrorNotif, message);
#endif
		}

		public static void LogException(Exception exception)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(ErrorNotif, exception);
#endif
		}
	}
}