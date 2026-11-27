namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEditor.Compilation;
    using UnityEngine;

    /// <summary>
    /// The project's Threadlink area: the <c>Threadlink User</c> folders and two assembly definitions.
    /// <c>Threadlink.User.Generated</c> compiles the generated IDs, so any assembly can reference them;
    /// <c>Threadlink.User</c> compiles the project's code in <c>Engineering/Codebase</c>. Both reference every installed
    /// Threadlink runtime assembly. The Hub's <b>Set Up Project</b> creates what is missing; generating IDs creates the
    /// generated assembly when no assembly definition owns its folder, and keeps its references complete.
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>The project's Threadlink folder.</summary>
        public const string Root = "Assets/Threadforge/Threadlink User";

        /// <summary>The project's code.</summary>
        public const string CodebaseFolder = Root + "/Engineering/Codebase";

        /// <summary>The assembly of the project's code.</summary>
        public const string UserAssembly = "Threadlink.User";

        /// <summary>The assembly of the generated IDs.</summary>
        public const string GeneratedAssembly = "Threadlink.User.Generated";

        // Packages whose assemblies a game does not build on: the sample packages.
        private static readonly string[] Samples = { "com.threadforge.threadlink.starter", "com.threadforge.threadlink.showcase" };

        // Unity assemblies the project's code commonly needs with Threadlink: UniTask for every asynchronous API,
        // Addressables for content scenes, Input System and Localization for Dextra. Each is added only when installed.
        private static readonly string[] Companions = { "UniTask", "Unity.Addressables", "Unity.ResourceManager", "Unity.InputSystem", "Unity.Localization" };

        /// <summary>The folders of the project area.</summary>
        public static IEnumerable<string> Folders =>
            new[]
            {
                CodebaseFolder,
                GeneratedIds.Folder,
                AssetLocations.EngineeringFolder,
                IdDomain.EngineeringFolder,
                AssetLocations.DesignFolder,
                IdDomain.DesignFolder,
                VaultPage.DefinitionsFolder,
            }.Distinct();

        /// <summary>What Set Up would change; empty when the project area is complete.</summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();
            var missing = Folders.Where(folder => !AssetDatabase.IsValidFolder(folder)).ToList();
            if (missing.Count > 0)
                problems.Add("Missing folders: " + string.Join(", ", missing.Select(folder => folder.Substring(Root.Length + 1))) + ".");
            if (OwnAssemblyDefinition(GeneratedIds.Folder) == null)
            {
                string owner = OwnerOf(GeneratedIds.Folder);
                problems.Add(
                    owner == null
                        ? "Generated IDs compile into Assembly-CSharp, which assemblies with their own definition cannot reference."
                        : $"Generated IDs compile into {Path.GetFileNameWithoutExtension(owner)}; they get their own assembly, {GeneratedAssembly}."
                );
            }
            if (OwnerOf(CodebaseFolder) == null)
                problems.Add("Project code in Engineering/Codebase compiles into Assembly-CSharp; it gets its own assembly, " + UserAssembly + ".");
            foreach (string asmdef in new[] { OwnerOf(GeneratedIds.Folder), OwnerOf(CodebaseFolder) }.Where(path => path != null).Distinct())
            {
                var data = Read(asmdef);
                var absent = RuntimeAssemblies().Where(assembly => !data.Contains(assembly.reference) && !data.Contains(assembly.name)).Select(assembly => assembly.name).ToList();
                if (absent.Count > 0)
                    problems.Add($"{Path.GetFileNameWithoutExtension(asmdef)} does not reference {string.Join(", ", absent)}.");
            }
            return problems;
        }

        /// <summary>
        /// Creates the missing folders and assembly definitions, and completes their references. An existing assembly
        /// definition that owns the codebase keeps its name; it gains a reference to the generated assembly.
        /// </summary>
        public static void SetUp()
        {
            foreach (string folder in Folders)
                AssetLocations.EnsureFolder(folder);
            var runtime = RuntimeAssemblies();
            string generated = OwnAssemblyDefinition(GeneratedIds.Folder);
            if (generated == null)
            {
                generated = GeneratedIds.Folder + "/" + GeneratedAssembly + ".asmdef";
                Write(generated, NewAssemblyDefinition(GeneratedAssembly, runtime.Select(assembly => assembly.reference)));
                // Imported now, so the codebase assembly can reference it by GUID.
                AssetDatabase.ImportAsset(generated, ImportAssetOptions.ForceSynchronousImport);
            }
            else
                Complete(generated, runtime);

            string user = OwnerOf(CodebaseFolder);
            var userReferences = runtime.Concat(Optional(Companions)).ToList();
            userReferences.Add((Reference(generated), Path.GetFileNameWithoutExtension(generated)));
            if (user == null)
            {
                string path = CodebaseFolder + "/" + UserAssembly + ".asmdef";
                Write(path, NewAssemblyDefinition(UserAssembly, userReferences.Select(assembly => assembly.reference)));
                AssetDatabase.ImportAsset(path);
            }
            else if (user != generated)
                Complete(user, userReferences);
        }

        /// <summary>
        /// Called before generated IDs are written: gives the generated folder its own assembly when no assembly
        /// definition owns it, and completes the owner's Threadlink references. Returns whether a file changed.
        /// </summary>
        internal static bool EnsureGeneratedAssembly()
        {
            string owner = OwnerOf(GeneratedIds.Folder);
            if (owner == null)
            {
                AssetLocations.EnsureFolder(GeneratedIds.Folder);
                string path = GeneratedIds.Folder + "/" + GeneratedAssembly + ".asmdef";
                Write(path, NewAssemblyDefinition(GeneratedAssembly, RuntimeAssemblies().Select(assembly => assembly.reference)));
                AssetDatabase.ImportAsset(path);
                return true;
            }
            return Complete(owner, RuntimeAssemblies());
        }

        /// <summary>Adds <paramref name="assemblies"/> to the references of an assembly definition file. Returns whether it changed.</summary>
        internal static bool Complete(string asmdef, IEnumerable<(string reference, string name)> assemblies)
        {
            string text = File.ReadAllText(asmdef);
            string updated = MergeReferences(text, assemblies);
            if (updated == text)
                return false;
            File.WriteAllText(asmdef, updated);
            AssetDatabase.ImportAsset(asmdef);
            return true;
        }

        /// <summary>
        /// The assembly definition text with every assembly in <paramref name="assemblies"/> referenced. An assembly counts
        /// as referenced by its GUID reference or its name. Other content and the file's indentation are kept.
        /// </summary>
        internal static string MergeReferences(string json, IEnumerable<(string reference, string name)> assemblies)
        {
            var present = Read(json, isText: true);
            var added = assemblies.Where(assembly => assembly.reference != null && !present.Contains(assembly.reference) && !present.Contains(assembly.name)).Select(assembly => assembly.reference).Distinct().ToList();
            if (added.Count == 0)
                return json;
            var references = present.Concat(added).ToList();
            int start = json.IndexOf("\"references\"", StringComparison.Ordinal);
            if (start < 0)
            {
                int brace = json.IndexOf('{');
                string inserted = "\n    \"references\": " + List(references, "    ") + ",";
                return json.Substring(0, brace + 1) + inserted + json.Substring(brace + 1);
            }
            int open = json.IndexOf('[', start);
            int close = json.IndexOf(']', open);
            int lineStart = json.LastIndexOf('\n', start) + 1;
            string indent = json.Substring(lineStart, start - lineStart);
            return json.Substring(0, open) + List(references, indent) + json.Substring(close + 1);
        }

        /// <summary>The text of a new assembly definition, in Unity's layout.</summary>
        internal static string NewAssemblyDefinition(string name, IEnumerable<string> references)
        {
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("    \"name\": \"").Append(name).Append("\",\n");
            text.Append("    \"rootNamespace\": \"\",\n");
            text.Append("    \"references\": ").Append(List(references.Where(reference => reference != null).Distinct().ToList(), "    ")).Append(",\n");
            text.Append("    \"includePlatforms\": [],\n");
            text.Append("    \"excludePlatforms\": [],\n");
            text.Append("    \"allowUnsafeCode\": false,\n");
            text.Append("    \"overrideReferences\": false,\n");
            text.Append("    \"precompiledReferences\": [],\n");
            text.Append("    \"autoReferenced\": true,\n");
            text.Append("    \"defineConstraints\": [],\n");
            text.Append("    \"versionDefines\": [],\n");
            text.Append("    \"noEngineReferences\": false\n");
            text.Append("}\n");
            return text.ToString();
        }

        private static string List(IReadOnlyCollection<string> references, string indent) =>
            references.Count == 0 ? "[]" : "[\n" + string.Join(",\n", references.Select(item => indent + indent + "\"" + item + "\"")) + "\n" + indent + "]";

        /// <summary>
        /// The main runtime assembly of every installed Threadlink package except the samples, as a GUID reference and a
        /// name: the assembly definition directly in each package's Runtime folder.
        /// </summary>
        internal static List<(string reference, string name)> RuntimeAssemblies() =>
            CompilationPipeline
                .GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Select(assembly => assembly.name)
                .Where(name => name.StartsWith("Threadforge.Threadlink", StringComparison.Ordinal))
                .Select(name => (name, path: CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name)))
                .Where(entry => IsPackageRuntime(entry.path))
                .OrderBy(entry => entry.name, StringComparer.Ordinal)
                .Select(entry => ("GUID:" + AssetDatabase.AssetPathToGUID(entry.path), entry.name))
                .ToList();

        /// <summary>Whether an assembly definition path is <c>Packages/&lt;Threadlink package&gt;/Runtime/*.asmdef</c>, outside the samples.</summary>
        internal static bool IsPackageRuntime(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            var parts = path.Replace('\\', '/').Split('/');
            return parts.Length == 4
                && parts[0] == "Packages"
                && parts[1].StartsWith("com.threadforge.threadlink", StringComparison.Ordinal)
                && !Samples.Contains(parts[1])
                && parts[2] == "Runtime";
        }

        private static IEnumerable<(string reference, string name)> Optional(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                if (!string.IsNullOrEmpty(path))
                    yield return (Reference(path), name);
            }
        }

        private static string Reference(string asmdefPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(asmdefPath);
            return string.IsNullOrEmpty(guid) ? Path.GetFileNameWithoutExtension(asmdefPath) : "GUID:" + guid;
        }

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
        }

        private static List<string> Read(string pathOrText, bool isText = false)
        {
            var data = JsonUtility.FromJson<AssemblyDefinition>(isText ? pathOrText : File.ReadAllText(pathOrText));
            return (data?.references ?? Array.Empty<string>()).ToList();
        }

        private static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        /// <summary>The assembly definition directly in <paramref name="folder"/>, or null.</summary>
        internal static string OwnAssemblyDefinition(string folder) =>
            Directory.Exists(folder) ? Directory.GetFiles(folder, "*.asmdef").Select(path => path.Replace('\\', '/')).FirstOrDefault() : null;

        /// <summary>The assembly definition that compiles <paramref name="folder"/>: its own or its nearest parent's under Assets, or null.</summary>
        internal static string OwnerOf(string folder)
        {
            for (string current = folder.Replace('\\', '/'); !string.IsNullOrEmpty(current) && current.StartsWith("Assets", StringComparison.Ordinal); current = Parent(current))
            {
                string own = OwnAssemblyDefinition(current);
                if (own != null)
                    return own;
                if (current == "Assets")
                    break;
            }
            return null;
        }

        private static string Parent(string folder)
        {
            int slash = folder.LastIndexOf('/');
            return slash < 0 ? null : folder.Substring(0, slash);
        }
    }
}

namespace Threadforge.Threadlink.Editor
{
    using System.Collections.Generic;

    /// <summary>Reports what the Hub's Set Up Project would create or complete.</summary>
    public sealed class ProjectSetupValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Project setup";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            foreach (string problem in ProjectSetup.Problems())
                issues.Add(new ValidationIssue(Name, problem.Contains(" does not reference ") ? IssueSeverity.Warning : IssueSeverity.Info, problem + " Run Set Up Project in the Hub."));
        }
    }
}
