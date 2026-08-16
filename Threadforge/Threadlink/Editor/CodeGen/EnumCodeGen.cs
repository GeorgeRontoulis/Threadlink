namespace Threadlink.Editor
{
    using System.Runtime.CompilerServices;
    using System.Text;

    internal static class EnumCodeGen
    {
        private static readonly StringBuilder stringBuilder = new();

        public static string SanitizeEnumName(string name)
        {
            // First character: must be letter or underscore
            char c = name[0];

            stringBuilder.Clear();
            stringBuilder.Append(IsIdentifierStart(c) ? c : '_');

            // Remaining characters: letter, digit, or underscore
            int length = name.Length;
            for (int i = 1; i < length; i++)
            {
                c = name[i];
                stringBuilder.Append(IsIdentifierPart(c) ? c : '_');
            }

            var output = stringBuilder.ToString();
            stringBuilder.Clear();
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
