namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Generates code from <see cref="IdDomain"/> assets: an enumeration per standalone domain, and one class per extended
    /// native domain. A domain that is deleted keeps its generated type with every member obsolete, so code that still
    /// uses it warns instead of breaking.
    /// </summary>
    public sealed class IdDomainGenerator : IIdGenerator
    {
        private const string RegistryFile = GeneratedIds.Folder + "/IdDomains.json";

        [Serializable]
        private sealed class Registry
        {
            public List<string> Enumerations = new List<string>();
            public List<string> Targets = new List<string>();
        }

        /// <inheritdoc />
        public string Title => "ID domains";

        /// <inheritdoc />
        public string Source => "ID Domain assets in Design/Identifiers and Engineering/Identifiers";

        /// <inheritdoc />
        public IEnumerable<string> Outputs => IdDomain.FindAll().Select(domain => domain.OutputName).Distinct();

        /// <inheritdoc />
        public void OpenSource() => WorkspaceWindow.Open().Show(typeof(CodeGenerationPage));

        /// <inheritdoc />
        public bool Generate()
        {
            var domains = IdDomain.FindAll();
            var registry = File.Exists(RegistryFile) ? JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryFile)) ?? new Registry() : new Registry();
            var enumerations = new List<string>();
            var targets = new List<string>();
            bool changed = false;
            foreach (var domain in domains.Where(domain => string.IsNullOrEmpty(domain.Extends)))
            {
                if (!Valid(domain))
                    continue;
                enumerations.Add(domain.TypeName);
                var members = domain.Entries.Select(entry => new GeneratedIds.Member
                {
                    Name = entry.Name,
                    Key = entry.Value.ToString(),
                    Value = entry.Value.ToString(),
                    Summary = entry.Summary,
                });
                changed |= GeneratedIds.WriteEnum(domain.TypeName, Describe(domain), members);
            }
            foreach (var group in domains.Where(domain => domain.Target != null && Valid(domain)).GroupBy(domain => domain.Target.Id))
            {
                var target = group.First().Target;
                targets.Add(target.ClassName);
                if (!string.IsNullOrEmpty(target.RequiredAssembly))
                    changed |= GeneratedIds.EnsureReference(target.RequiredAssembly);
                var members = group.SelectMany(domain => domain.Entries.Select(entry => target.Member(domain, entry)));
                changed |= GeneratedIds.Write(target.ClassName, target.Summary, members);
            }
            // Types whose domains were deleted stay, with every member obsolete.
            foreach (string removed in registry.Enumerations.Except(enumerations))
                if (File.Exists(GeneratedIds.PathOf(removed)))
                    changed |= GeneratedIds.WriteEnum(removed, "Removed ID domain. Every member is obsolete.", Array.Empty<GeneratedIds.Member>());
            foreach (string removed in registry.Targets.Except(targets))
                if (File.Exists(GeneratedIds.PathOf(removed)))
                    changed |= GeneratedIds.Write(removed, "No ID domain extends this class any more. Every member is obsolete.", Array.Empty<GeneratedIds.Member>());
            var next = new Registry
            {
                Enumerations = registry.Enumerations.Union(enumerations).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                Targets = registry.Targets.Union(targets).OrderBy(name => name, StringComparer.Ordinal).ToList(),
            };
            if (domains.Count > 0 || File.Exists(RegistryFile))
            {
                Directory.CreateDirectory(GeneratedIds.Folder);
                File.WriteAllText(RegistryFile, JsonUtility.ToJson(next, true));
            }
            return changed;
        }

        private static bool Valid(IdDomain domain)
        {
            var problems = domain.Validate();
            foreach (string problem in problems)
                Debug.LogWarning("Threadlink: ID domain " + domain.name + " was not generated. " + problem, domain);
            return problems.Count == 0;
        }

        private static string Describe(IdDomain domain) =>
            string.IsNullOrWhiteSpace(domain.Summary) ? domain.name + " IDs (" + domain.Owner.ToString().ToLowerInvariant() + "-owned ID domain)." : domain.Summary.Trim();
    }
}
