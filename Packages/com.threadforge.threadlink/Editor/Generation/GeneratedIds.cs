namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// A source of generated, typed IDs, such as input actions or Build Profile scenes. Discovered with TypeCache and
    /// listed on the Workspace's Code Generation page.
    /// </summary>
    public interface IIdGenerator
    {
        /// <summary>Writes the generated class. Return false when there is nothing to generate yet.</summary>
        bool Generate();

        /// <summary>Name shown on the Code Generation page.</summary>
        string Title => GetType().Name;

        /// <summary>Where the IDs come from, in a few words.</summary>
        string Source => string.Empty;

        /// <summary>The generated class names.</summary>
        IEnumerable<string> Outputs => Array.Empty<string>();

        /// <summary>Opens the place the IDs are authored, such as a Project Settings page.</summary>
        void OpenSource() { }
    }

    /// <summary>
    /// Writes generated ID classes into the project's <see cref="Folder"/>. Each member keeps a stable key; when a key
    /// disappears its member stays as an [Obsolete] tombstone (recorded in a ledger next to the file), so code that still
    /// uses it warns instead of silently breaking.
    /// </summary>
    public static class GeneratedIds
    {
        /// <summary>Where generated classes are written.</summary>
        public const string Folder = "Assets/Threadforge/Threadlink User/Engineering/Codebase/Generated";

        /// <summary>One generated member.</summary>
        [Serializable]
        public sealed class Member
        {
            /// <summary>Nested class holding the member, or empty for the top level. Enumerations have no groups.</summary>
            public string Group = string.Empty;

            /// <summary>Member name; sanitized into a C# identifier.</summary>
            public string Name;

            /// <summary>Member type, fully qualified. Unused for enumerations.</summary>
            public string Type;

            /// <summary>Stable key: a GUID, name or value that identifies the source across renames.</summary>
            public string Key;

            /// <summary>Initializer expression, or the constant of an enumeration member.</summary>
            public string Value;

            /// <summary>Optional documentation.</summary>
            public string Summary;
        }

        [Serializable]
        private sealed class Ledger
        {
            public List<Member> Members = new List<Member>();
        }

        private enum Layout
        {
            Class,
            Enumeration,
        }

        private static bool requested;

        /// <summary>Generates a static class of typed members. Returns whether the file changed.</summary>
        public static bool Write(string className, string summary, IEnumerable<Member> members, string footer = null) =>
            WriteTo(Folder, className, summary, members, footer);

        /// <summary>
        /// Generates an <c>int</c> enumeration with <c>None = 0</c>. Member values are the keys, so they stay stable across
        /// renames and removed values remain as obsolete tombstones. Returns whether the file changed.
        /// </summary>
        public static bool WriteEnum(string enumName, string summary, IEnumerable<Member> members) =>
            WriteTo(Folder, enumName, summary, members, null, Layout.Enumeration);

        /// <summary>Keys recorded for a generated class, including removed (tombstoned) ones. Never reuse them.</summary>
        public static HashSet<string> RecordedKeys(string className) => RecordedKeys(Folder, className);

        /// <summary>Path of a generated class.</summary>
        public static string PathOf(string className) => Folder + "/" + className + ".cs";

        internal static HashSet<string> RecordedKeys(string folder, string className)
        {
            var ledger = ReadLedger(folder + "/" + className + ".cs.ids.json");
            return new HashSet<string>(ledger.Members.Select(member => member.Key));
        }

        /// <summary>Members of a generated class as recorded in its ledger, and how many are tombstones.</summary>
        public static (int live, int tombstones) Count(string className)
        {
            string path = PathOf(className);
            if (!File.Exists(path))
                return (0, 0);
            var ledger = ReadLedger(path + ".ids.json");
            int tombstones = File.ReadAllText(path).Split('\n').Count(line => line.Contains("[global::System.Obsolete("));
            return (ledger.Members.Count - tombstones, tombstones);
        }

        internal static bool WriteTo(string folder, string className, string summary, IEnumerable<Member> members, string footer = null) =>
            WriteTo(folder, className, summary, members, footer, Layout.Class);

        internal static bool WriteEnumTo(string folder, string enumName, string summary, IEnumerable<Member> members) =>
            WriteTo(folder, enumName, summary, members, null, Layout.Enumeration);

        private static bool WriteTo(string folder, string className, string summary, IEnumerable<Member> members, string footer, Layout layout)
        {
            var current = members.Select(Clean).ToList();
            string path = folder + "/" + className + ".cs";
            string ledgerPath = path + ".ids.json";
            var ledger = ReadLedger(ledgerPath);
            var keys = new HashSet<string>(current.Select(member => member.Group + "|" + member.Key));
            var names = new HashSet<string>(current.Select(member => member.Group + "|" + member.Name));
            var tombstones = ledger
                .Members.Where(old => !keys.Contains(old.Group + "|" + old.Key) && !names.Contains(old.Group + "|" + old.Name))
                .ToList();
            var all = current.Concat(tombstones).ToList();
            var code = new StringBuilder();
            code.AppendLine("// <auto-generated>Threadlink generated IDs. Regenerate with Threadlink ▸ Generate IDs; do not edit.</auto-generated>");
            code.AppendLine("#pragma warning disable CS0618");
            code.AppendLine();
            code.AppendLine("/// <summary>" + Escape(summary) + "</summary>");
            if (layout == Layout.Enumeration)
                WriteEnumeration(code, className, current, tombstones);
            else
                WriteClass(code, className, all, tombstones, footer);
            Directory.CreateDirectory(folder);
            string text = code.ToString().Replace("\r\n", "\n");
            File.WriteAllText(ledgerPath, JsonUtility.ToJson(new Ledger { Members = all }, true));
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text)
                return false;
            File.WriteAllText(path, text);
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.ImportAsset(path);
            return true;
        }

        private static void WriteClass(StringBuilder code, string className, List<Member> all, List<Member> tombstones, string footer)
        {
            code.AppendLine("public static partial class " + className);
            code.AppendLine("{");
            foreach (var group in all.GroupBy(member => member.Group).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                string indent = "    ";
                if (group.Key.Length > 0)
                {
                    code.AppendLine("    public static class " + group.Key);
                    code.AppendLine("    {");
                    indent = "        ";
                }
                foreach (var member in group.OrderBy(member => tombstones.Contains(member)).ThenBy(member => member.Name, StringComparer.Ordinal))
                {
                    if (!string.IsNullOrEmpty(member.Summary))
                        code.Append(indent).AppendLine("/// <summary>" + Escape(member.Summary) + "</summary>");
                    if (tombstones.Contains(member))
                        code.Append(indent).AppendLine(Obsolete);
                    code.Append(indent).Append("public static readonly ").Append(member.Type).Append(' ').Append(member.Name).Append(" = ").Append(member.Value).AppendLine(";");
                }
                if (group.Key.Length > 0)
                    code.AppendLine("    }");
            }
            if (!string.IsNullOrEmpty(footer))
                code.Append(footer);
            code.AppendLine("}");
        }

        // Authored order is kept: an enumeration reads like the list it came from. Tombstones follow the live members.
        private static void WriteEnumeration(StringBuilder code, string enumName, List<Member> current, List<Member> tombstones)
        {
            code.AppendLine("public enum " + enumName + " : int");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>No value.</summary>");
            code.AppendLine("    None = 0,");
            foreach (var member in current.Concat(tombstones))
            {
                if (!string.IsNullOrEmpty(member.Summary))
                    code.AppendLine("    /// <summary>" + Escape(member.Summary) + "</summary>");
                if (tombstones.Contains(member))
                    code.AppendLine("    " + Obsolete);
                code.Append("    ").Append(member.Name).Append(" = ").Append(member.Value).AppendLine(",");
            }
            code.AppendLine("}");
        }

        private const string Obsolete = "[global::System.Obsolete(\"This ID no longer exists in its source.\")]";

        private static Ledger ReadLedger(string ledgerPath) =>
            File.Exists(ledgerPath) ? JsonUtility.FromJson<Ledger>(File.ReadAllText(ledgerPath)) ?? new Ledger() : new Ledger();

        /// <summary>
        /// Makes the assembly that compiles <see cref="Folder"/> reference <paramref name="assembly"/>, for generated code
        /// that uses an optional package. Returns whether the assembly definition changed.
        /// </summary>
        public static bool EnsureReference(string assembly)
        {
            string asmdef = FindAssemblyDefinition(Folder);
            if (asmdef == null)
                return false;
            // Reference by GUID ("Use GUIDs"), as every Threadlink assembly definition does, so renaming an assembly
            // never breaks the reference. A name is used only when the assembly has no definition file.
            string target = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly);
            string guid = string.IsNullOrEmpty(target) ? null : AssetDatabase.AssetPathToGUID(target);
            string reference = string.IsNullOrEmpty(guid) ? assembly : "GUID:" + guid;
            string text = File.ReadAllText(asmdef);
            var data = JsonUtility.FromJson<AssemblyDefinition>(text);
            var references = (data.references ?? Array.Empty<string>()).ToList();
            if (references.Contains(reference) || references.Contains(assembly))
                return false;
            references.Add(reference);
            int start = text.IndexOf("\"references\"", StringComparison.Ordinal);
            if (start < 0)
                return false;
            int open = text.IndexOf('[', start);
            int close = text.IndexOf(']', open);
            // Keep the file's own indentation.
            int lineStart = text.LastIndexOf('\n', start) + 1;
            string indent = text.Substring(lineStart, start - lineStart);
            string list = "[\n" + string.Join(",\n", references.Select(item => indent + indent + "\"" + item + "\"")) + "\n" + indent + "]";
            File.WriteAllText(asmdef, text.Substring(0, open) + list + text.Substring(close + 1));
            AssetDatabase.ImportAsset(asmdef);
            return true;
        }

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
        }

        private static string FindAssemblyDefinition(string folder)
        {
            for (var directory = new DirectoryInfo(folder); directory != null; directory = directory.Parent)
            {
                var found = directory.GetFiles("*.asmdef").FirstOrDefault();
                if (found != null)
                    return Path.GetRelativePath(Directory.GetCurrentDirectory(), found.FullName).Replace('\\', '/');
                if (directory.Name == "Assets")
                    break;
            }
            return null;
        }

        /// <summary>A C# identifier for <paramref name="name"/>: words joined in PascalCase, never starting with a digit.</summary>
        public static string Identifier(string name)
        {
            var text = new StringBuilder();
            bool upper = true;
            foreach (char character in name ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    text.Append(upper ? char.ToUpperInvariant(character) : character);
                    upper = false;
                }
                else
                    upper = true;
            }
            if (text.Length == 0)
                text.Append("Unnamed");
            if (char.IsDigit(text[0]))
                text.Insert(0, '_');
            return text.ToString();
        }

        /// <summary>A C# string literal.</summary>
        public static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>Every generator in the project, in title order.</summary>
        public static List<IIdGenerator> Generators() =>
            TypeCache
                .GetTypesDerivedFrom<IIdGenerator>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (IIdGenerator)Activator.CreateInstance(type))
                .OrderBy(generator => generator.Title, StringComparer.Ordinal)
                .ToList();

        /// <summary>Runs every generator now.</summary>
        [MenuItem("Threadlink/Generate IDs", priority = 20)]
        public static void GenerateAll()
        {
            requested = false;
            foreach (var generator in Generators())
                Run(generator);
        }

        /// <summary>Runs one generator, logging instead of throwing when its source is invalid.</summary>
        public static bool Run(IIdGenerator generator)
        {
            try
            {
                return generator.Generate();
            }
            catch (Exception error)
            {
                Debug.LogError("Threadlink: " + generator.Title + " could not generate IDs. " + error.Message);
                return false;
            }
        }

        /// <summary>Schedules generation once, after the current import or edit.</summary>
        public static void Request()
        {
            if (requested)
                return;
            requested = true;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                {
                    requested = false;
                    Request();
                    return;
                }
                GenerateAll();
            };
        }

        private static Member Clean(Member member)
        {
            member.Group = member.Group ?? string.Empty;
            member.Name = Identifier(member.Name);
            return member;
        }

        private static string Escape(string text) => (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>Regenerates IDs when their sources change: scenes, input actions, ID domains, engineering settings and the build scene list.</summary>
    internal sealed class GeneratedIdSources : AssetPostprocessor
    {
        [InitializeOnLoadMethod]
        private static void Watch()
        {
            EditorBuildSettings.sceneListChanged -= GeneratedIds.Request;
            EditorBuildSettings.sceneListChanged += GeneratedIds.Request;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(deleted).Concat(moved).Concat(movedFrom))
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(ThreadlinkSettingsAssets.Folder, StringComparison.Ordinal)
                    || IdDomain.IsDomainPath(path) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    GeneratedIds.Request();
                    return;
                }
        }
    }
}
