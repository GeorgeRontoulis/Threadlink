namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Reads and edits the project's <c>Packages/manifest.json</c>. The Package Manager has no public API for scoped
    /// registries, so the Hub edits the manifest directly and keeps every other entry as it was.
    /// </summary>
    public static class ProjectManifest
    {
        /// <summary>Default manifest path.</summary>
        public const string Path = "Packages/manifest.json";

        /// <summary>Whether a scoped registry with this URL covers every given scope.</summary>
        public static bool HasRegistry(string json, string url, IEnumerable<string> scopes)
        {
            var root = (Dictionary<string, object>)Json.Parse(json);
            var registry = Registries(root).FirstOrDefault(r => SameUrl(r, url));
            if (registry == null)
                return false;
            var present = registry.TryGetValue("scopes", out var value) && value is List<object> list ? list.OfType<string>() : Enumerable.Empty<string>();
            return scopes.All(scope => present.Any(p => scope == p || scope.StartsWith(p + ".", StringComparison.Ordinal)));
        }

        /// <summary>Adds the registry, or the missing scopes to an existing entry with the same URL. Returns the new JSON.</summary>
        public static string AddRegistry(string json, string name, string url, IEnumerable<string> scopes)
        {
            var root = (Dictionary<string, object>)Json.Parse(json);
            if (!root.TryGetValue("scopedRegistries", out var value) || !(value is List<object> registries))
            {
                registries = new List<object>();
                root["scopedRegistries"] = registries;
            }
            var registry = registries.OfType<Dictionary<string, object>>().FirstOrDefault(r => SameUrl(r, url));
            if (registry == null)
            {
                registry = new Dictionary<string, object> { ["name"] = name, ["url"] = url, ["scopes"] = new List<object>() };
                registries.Add(registry);
            }
            if (!registry.TryGetValue("scopes", out var existing) || !(existing is List<object> list))
                registry["scopes"] = list = new List<object>();
            foreach (var scope in scopes)
                if (!list.OfType<string>().Contains(scope))
                    list.Add(scope);
            return Json.Write(root) + "\n";
        }

        /// <summary>Adds the registry to the project's manifest file. Returns false when nothing needed to change.</summary>
        public static bool EnsureRegistry(string name, string url, IEnumerable<string> scopes, string path = Path)
        {
            string json = File.ReadAllText(path);
            var required = scopes.ToList();
            if (HasRegistry(json, url, required))
                return false;
            File.WriteAllText(path, AddRegistry(json, name, url, required));
            return true;
        }

        private static IEnumerable<Dictionary<string, object>> Registries(Dictionary<string, object> root) =>
            root.TryGetValue("scopedRegistries", out var value) && value is List<object> list
                ? list.OfType<Dictionary<string, object>>()
                : Enumerable.Empty<Dictionary<string, object>>();

        private static bool SameUrl(Dictionary<string, object> registry, string url) =>
            registry.TryGetValue("url", out var value) && value is string text && string.Equals(text.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        /// <summary>Minimal JSON reader and writer preserving member order.</summary>
        internal static class Json
        {
            internal static object Parse(string text)
            {
                int index = 0;
                object value = ReadValue(text, ref index);
                SkipWhitespace(text, ref index);
                if (index != text.Length)
                    throw new FormatException("Unexpected content after the JSON value.");
                return value;
            }

            internal static string Write(object value)
            {
                var builder = new StringBuilder();
                WriteValue(builder, value, 0);
                return builder.ToString();
            }

            private static object ReadValue(string text, ref int index)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                    throw new FormatException("Unexpected end of JSON.");
                char c = text[index];
                if (c == '{')
                {
                    var result = new Dictionary<string, object>();
                    index++;
                    SkipWhitespace(text, ref index);
                    if (text[index] == '}')
                    {
                        index++;
                        return result;
                    }
                    while (true)
                    {
                        SkipWhitespace(text, ref index);
                        string key = ReadString(text, ref index);
                        SkipWhitespace(text, ref index);
                        Expect(text, ref index, ':');
                        result[key] = ReadValue(text, ref index);
                        SkipWhitespace(text, ref index);
                        if (text[index] == ',')
                        {
                            index++;
                            continue;
                        }
                        Expect(text, ref index, '}');
                        return result;
                    }
                }
                if (c == '[')
                {
                    var result = new List<object>();
                    index++;
                    SkipWhitespace(text, ref index);
                    if (text[index] == ']')
                    {
                        index++;
                        return result;
                    }
                    while (true)
                    {
                        result.Add(ReadValue(text, ref index));
                        SkipWhitespace(text, ref index);
                        if (text[index] == ',')
                        {
                            index++;
                            continue;
                        }
                        Expect(text, ref index, ']');
                        return result;
                    }
                }
                if (c == '"')
                    return ReadString(text, ref index);
                if (text.Length - index >= 4 && string.CompareOrdinal(text, index, "true", 0, 4) == 0)
                {
                    index += 4;
                    return true;
                }
                if (text.Length - index >= 5 && string.CompareOrdinal(text, index, "false", 0, 5) == 0)
                {
                    index += 5;
                    return false;
                }
                if (text.Length - index >= 4 && string.CompareOrdinal(text, index, "null", 0, 4) == 0)
                {
                    index += 4;
                    return null;
                }
                int start = index;
                while (index < text.Length && "+-0123456789.eE".IndexOf(text[index]) >= 0)
                    index++;
                if (start == index)
                    throw new FormatException("Unexpected character '" + c + "' in JSON.");
                return double.Parse(text.Substring(start, index - start), CultureInfo.InvariantCulture);
            }

            private static string ReadString(string text, ref int index)
            {
                Expect(text, ref index, '"');
                var builder = new StringBuilder();
                while (text[index] != '"')
                {
                    char c = text[index++];
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }
                    char escape = text[index++];
                    switch (escape)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'u':
                            builder.Append((char)int.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default: builder.Append(escape); break;
                    }
                }
                index++;
                return builder.ToString();
            }

            private static void WriteValue(StringBuilder builder, object value, int depth)
            {
                switch (value)
                {
                    case null:
                        builder.Append("null");
                        break;
                    case bool flag:
                        builder.Append(flag ? "true" : "false");
                        break;
                    case double number:
                        builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                        break;
                    case string text:
                        WriteString(builder, text);
                        break;
                    case Dictionary<string, object> map:
                        if (map.Count == 0)
                        {
                            builder.Append("{}");
                            break;
                        }
                        builder.Append("{\n");
                        int i = 0;
                        foreach (var pair in map)
                        {
                            builder.Append(' ', (depth + 1) * 2);
                            WriteString(builder, pair.Key);
                            builder.Append(": ");
                            WriteValue(builder, pair.Value, depth + 1);
                            builder.Append(++i < map.Count ? ",\n" : "\n");
                        }
                        builder.Append(' ', depth * 2).Append('}');
                        break;
                    case List<object> list:
                        if (list.Count == 0)
                        {
                            builder.Append("[]");
                            break;
                        }
                        builder.Append("[\n");
                        for (int j = 0; j < list.Count; j++)
                        {
                            builder.Append(' ', (depth + 1) * 2);
                            WriteValue(builder, list[j], depth + 1);
                            builder.Append(j + 1 < list.Count ? ",\n" : "\n");
                        }
                        builder.Append(' ', depth * 2).Append(']');
                        break;
                    default:
                        throw new FormatException("Unsupported JSON value " + value.GetType().Name);
                }
            }

            private static void WriteString(StringBuilder builder, string text)
            {
                builder.Append('"');
                foreach (char c in text)
                {
                    switch (c)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (c < ' ')
                                builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                builder.Append(c);
                            break;
                    }
                }
                builder.Append('"');
            }

            private static void SkipWhitespace(string text, ref int index)
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            private static void Expect(string text, ref int index, char expected)
            {
                if (index >= text.Length || text[index] != expected)
                    throw new FormatException($"Expected '{expected}' at position {index}.");
                index++;
            }
        }
    }
}
