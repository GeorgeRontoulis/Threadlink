namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal static class ThreadlinkDomainSources
    {
        internal static void Gather(ThreadlinkDomainDescriptor descriptor, List<ThreadlinkDomainEntry> buffer)
        {
            buffer.Clear();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int sourceCount = descriptor.Sources.Count;

            for (int i = 0; i < sourceCount; i++)
            {
                var source = descriptor.Sources[i];

                if (File.Exists(source.AbsolutePath) is false)
                    continue;

                var lines = File.ReadAllLines(source.AbsolutePath);
                int lineCount = lines.Length;

                for (int j = 0; j < lineCount; j++)
                {
                    var line = lines[j].Trim();

                    if (IsIgnorable(line))
                        continue;

                    var key = ComposeKey(source.Scope, line);

                    if (seen.Add(key) is false)
                        continue;

                    buffer.Add(new ThreadlinkDomainEntry(key, line, source.Scope));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ComposeKey(string scope, string name)
        {
            return string.IsNullOrEmpty(scope) ? name : string.Concat(scope, "/", name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIgnorable(string line)
        {
            return string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal);
        }
    }
}
