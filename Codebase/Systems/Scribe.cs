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
#if UNITY_EDITOR && THREADLINK_SCRIBE
		[UnityEngine.SerializeField] private bool pauseOnSystemLog = false;
#endif

		public override void Boot() { }
		public override void Initialize() { Instance = this; }

		public static void SystemLog(string systemID, DebugNotificationType logType, params string[] message)
		{
#if THREADLINK_SCRIBE
			string prefix = String.Construct("[", systemID, "] - ");
			string decodedMessage = String.Construct(message);

			switch (logType)
			{
				case DebugNotificationType.Info:
				LogInfo(prefix, decodedMessage);
				break;
				case DebugNotificationType.Warning:
				LogWarning(prefix, decodedMessage);
				break;
				case DebugNotificationType.Error:
				LogError(prefix, decodedMessage);
				break;
			}

#if UNITY_EDITOR
			if (Instance != null && Instance.pauseOnSystemLog)
				UnityEditor.EditorApplication.isPaused = true;
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
			UnityConsole.Notify(DebugNotificationType.Warning, message);
#endif
		}

		public static void LogError(params string[] message)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(DebugNotificationType.Error, message);
#endif
		}

		public static void LogException(Exception exception)
		{
#if THREADLINK_SCRIBE
			UnityConsole.Notify(DebugNotificationType.Error, exception);
#endif
		}
	}
}