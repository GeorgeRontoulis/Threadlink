namespace Threadlink.Core.NativeSubsystems.Scribe
{
    using Cysharp.Text;

    /// <summary>
    /// Threadlink's Logging Subsystem.
    /// </summary>
    public static partial class Scribe
    {
        public static Utf8ValueStringBuilder ToNonAllocText(params object[] input)
        {
            using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
            int length = input.Length;

            for (int i = 0; i < length; i++) stringBuilder.Append(input[i]);

            return stringBuilder;
        }

        public static Utf8ValueStringBuilder Send<T>(params object[] message)
        {
            using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
            int length = message.Length;

            stringBuilder.Append("[");
            stringBuilder.Append(typeof(T).Name);
            stringBuilder.Append("] - ");

            for (int i = 0; i < length; i++) stringBuilder.Append(message[i]);

            return stringBuilder;
        }
    }
}