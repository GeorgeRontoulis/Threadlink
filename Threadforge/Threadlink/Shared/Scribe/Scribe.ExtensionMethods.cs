namespace Threadlink.Core.NativeSubsystems.Scribe
{
    using Cysharp.Text;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    public enum DebugType : byte { Info, Warning, Error }

    public static partial class Scribe
    {
        public static Utf8ValueStringBuilder Send<T>(this T source, params object[] message)
        {
            using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
            int length = message.Length;

            stringBuilder.Append("[");
            stringBuilder.Append(source.GetType().Name);
            stringBuilder.Append("] - ");

            for (int i = 0; i < length; i++)
                stringBuilder.Append(message[i]);

            return stringBuilder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToUnityConsole(this Utf8ValueStringBuilder nonAllocInput, DebugType logType = DebugType.Info)
        {
            string loggedMessage = nonAllocInput.ToString();

            switch (logType)
            {
                case DebugType.Info:
                    Debug.Log(loggedMessage);
                    break;
                case DebugType.Warning:
                    Debug.LogWarning(loggedMessage);
                    break;
                case DebugType.Error:
                    Debug.LogError(loggedMessage);
                    break;
            }
        }
    }
}