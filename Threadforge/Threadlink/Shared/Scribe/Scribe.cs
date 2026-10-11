namespace Threadlink.Core.NativeSubsystems.Scribe
{
    using Cysharp.Text;
    using Shared;
    using System.Text;
    using Unity.Scripting.LifecycleManagement;

    /// <summary>
    /// Threadlink's Logging Subsystem.
    /// </summary>
    public static partial class Scribe
    {
        private const int RECENT_CAPACITY = 6;

        [NoAutoStaticsCleanup] private static readonly object CountLock = new();
        [NoAutoStaticsCleanup] private static readonly string[] RecentMessages = new string[RECENT_CAPACITY];
        [NoAutoStaticsCleanup] private static readonly DebugType[] RecentTypes = new DebugType[RECENT_CAPACITY];
        [NoAutoStaticsCleanup] private static readonly int[] Counts = new int[3];
        [NoAutoStaticsCleanup] private static int recentNext, recentCount;

        public static Utf8ValueStringBuilder Send<T>(params object[] message)
        {
            using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
            int length = message.Length;

            stringBuilder.Append("<color=cyan>[");
            stringBuilder.Append(typeof(T).Name);
            stringBuilder.Append("] - </color>");

            for (int i = 0; i < length; i++) stringBuilder.Append(message[i]);

            return stringBuilder;
        }

        /// <summary>Counts a message sent to the console and keeps it among the recent ones. Costs a lock and two stores.</summary>
        private static void Record(DebugType type, string message)
        {
            lock (CountLock)
            {
                Counts[(int)type]++;
                RecentMessages[recentNext] = message;
                RecentTypes[recentNext] = type;
                recentNext = (recentNext + 1) % RECENT_CAPACITY;

                if (recentCount < RECENT_CAPACITY)
                    recentCount++;
            }
        }

        [OnExitingPlayMode]
        private static void ResetRecord()
        {
            lock (CountLock)
            {
                Counts[0] = Counts[1] = Counts[2] = 0;
                System.Array.Clear(RecentMessages, 0, RECENT_CAPACITY);
                recentNext = recentCount = 0;
            }
        }

        [Monitored("Scribe", Description = "Formats Threadlink's log messages and sends them to the Unity console.", Order = -997)]
        private static void Report(MonitorReport report)
        {
            int infos, warnings, errors, count;
            string[] messages = null;
            DebugType[] types = null;

            lock (CountLock)
            {
                infos = Counts[(int)DebugType.Info];
                warnings = Counts[(int)DebugType.Warning];
                errors = Counts[(int)DebugType.Error];
                count = recentCount;

                if (report.Detailed)
                {
                    messages = new string[count];
                    types = new DebugType[count];

                    // Newest first.
                    for (int i = 0; i < count; i++)
                    {
                        int index = (recentNext - 1 - i + RECENT_CAPACITY) % RECENT_CAPACITY;
                        messages[i] = RecentMessages[index];
                        types[i] = RecentTypes[index];
                    }
                }
            }

            report.Summary(errors > 0 ? errors + (errors == 1 ? " error" : " errors") : infos + warnings + " messages", errors > 0);

            if (!report.Detailed)
                return;

            report.Section("Messages this session")
                .Field("Info", infos)
                .Field("Warnings", warnings, warnings > 0 ? MonitorTone.Caution : MonitorTone.Normal)
                .Field("Errors", errors, errors > 0 ? MonitorTone.Bad : MonitorTone.Normal);

            report.Section("Recent");

            if (count == 0)
                report.Note("None yet.");

            for (int i = 0; i < count; i++)
            {
                var tone = types[i] switch
                {
                    DebugType.Warning => MonitorTone.Caution,
                    DebugType.Error => MonitorTone.Bad,
                    _ => MonitorTone.Muted,
                };

                report.Note(StripRichText(messages[i]), tone);
            }
        }

        /// <summary><paramref name="text"/> without rich-text tags, on one line.</summary>
        private static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            bool inTag = false;

            foreach (char character in text)
            {
                if (character == '<') inTag = true;
                else if (character == '>' && inTag) inTag = false;
                else if (!inTag)
                {
                    if (character == '\n' || character == '\r')
                        break;

                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
