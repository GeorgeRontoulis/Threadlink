namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Who maintains an ID domain. It decides the folder the domain lives in.</summary>
    public enum IdDomainOwner
    {
        /// <summary>Designers add and rename the entries, for example enemy kinds or spawn points.</summary>
        Design,

        /// <summary>Engineers maintain the entries, for example random domains or internal channels.</summary>
        Engineering,
    }

    /// <summary>
    /// A named list of IDs that Threadlink turns into code. On its own, a domain generates an <c>int</c> enumeration
    /// named after the asset (<c>None = 0</c>, then one member per entry). A domain can instead extend a native domain,
    /// such as StatelessRNG random domains or Vault fields, whose members join that domain's generated class.
    /// </summary>
    /// <remarks>
    /// Entry values are assigned once and kept: renaming an entry keeps its value, so serialized data and saved games
    /// stay valid. A removed entry stays in the generated code as an obsolete tombstone and its value is never reused.
    /// </remarks>
    [CreateAssetMenu(menuName = "Threadlink/ID Domain", order = 0)]
    public sealed class IdDomain : ScriptableObject
    {
        /// <summary>The folder for design-owned domains.</summary>
        public const string DesignFolder = "Assets/Threadforge/Threadlink User/Design/Identifiers";

        /// <summary>The folder for engineering-owned domains.</summary>
        public const string EngineeringFolder = "Assets/Threadforge/Threadlink User/Engineering/Identifiers";

        /// <summary>One ID.</summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>The name; becomes the member name in PascalCase.</summary>
            [Tooltip("Member name in code, written in PascalCase.")]
            public string Name = string.Empty;

            /// <summary>The stable value. Assigned when the entry is added and kept across renames.</summary>
            [Tooltip("Stable value, assigned once. Renaming keeps it.")]
            public int Value;

            /// <summary>Optional description, shown in code completion.</summary>
            [Tooltip("Optional description shown in code completion.")]
            public string Summary = string.Empty;
        }

        [Tooltip("Who maintains it. Decides its folder: Design or Engineering/Identifiers.")]
        [SerializeField]
        private IdDomainOwner owner;

        [Tooltip("Generate its own enumeration, or add its entries to a native domain.")]
        [SerializeField]
        private string extends = string.Empty;

        [Tooltip("What the IDs identify; becomes the generated type's documentation.")]
        [SerializeField, TextArea(1, 3)]
        private string summary = string.Empty;

        [SerializeField]
        [Tooltip("The IDs. Renaming keeps values; removing leaves a tombstone.")]
        private List<Entry> entries = new List<Entry>();

        /// <summary>Who maintains this domain.</summary>
        public IdDomainOwner Owner => owner;

        /// <summary>The native domain this domain extends, or empty for its own enumeration.</summary>
        public string Extends => extends ?? string.Empty;

        /// <summary>What the IDs identify.</summary>
        public string Summary => summary ?? string.Empty;

        /// <summary>The entries, in authored order.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>The generated enumeration's name, from the asset name.</summary>
        public string TypeName => GeneratedIds.Identifier(name);

        /// <summary>The native domain this domain extends, or null.</summary>
        public IIdDomainTarget Target => IdDomainTargets.Find(Extends);

        /// <summary>Whether a path is inside one of the ID domain folders.</summary>
        public static bool IsDomainPath(string path) =>
            path.StartsWith(DesignFolder + "/", StringComparison.Ordinal) || path.StartsWith(EngineeringFolder + "/", StringComparison.Ordinal);

        /// <summary>Every ID domain in the project, by name.</summary>
        public static List<IdDomain> FindAll() =>
            AssetDatabase
                .FindAssets("t:" + nameof(IdDomain))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<IdDomain>)
                .Where(domain => domain != null)
                .OrderBy(domain => domain.name, StringComparer.Ordinal)
                .ToList();

        /// <summary>Creates a domain asset in its owner's folder and returns it.</summary>
        public static IdDomain Create(string domainName, IdDomainOwner owner, string extends = "")
        {
            string typeName = GeneratedIds.Identifier(domainName);
            string folder = owner == IdDomainOwner.Design ? DesignFolder : EngineeringFolder;
            Directory.CreateDirectory(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + typeName + ".asset");
            var domain = CreateInstance<IdDomain>();
            domain.owner = owner;
            domain.extends = extends ?? string.Empty;
            AssetDatabase.CreateAsset(domain, path);
            AssetDatabase.SaveAssets();
            return domain;
        }

        /// <summary>Adds an entry with a fresh stable value. Returns the entry.</summary>
        public Entry Add(string entryName, string entrySummary = "")
        {
            var entry = new Entry { Name = entryName ?? string.Empty, Summary = entrySummary ?? string.Empty };
            entries.Add(entry);
            AssignValues();
            EditorUtility.SetDirty(this);
            return entry;
        }

        /// <summary>The class an entry's member is generated into.</summary>
        public string OutputName => Target != null ? Target.ClassName : TypeName;

        /// <summary>Problems that stop generation, or an empty list.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (!string.IsNullOrEmpty(Extends) && Target == null)
                problems.Add($"Extends '{Extends}', which is not installed.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                string member = GeneratedIds.Identifier(entry.Name);
                if (string.IsNullOrWhiteSpace(entry.Name))
                    problems.Add("An entry has no name.");
                else if (member == "None" && Target == null)
                    problems.Add("'None' is reserved for the empty value.");
                else if (!seen.Add(member))
                    problems.Add($"Two entries are both named {member}.");
            }
            return problems;
        }

        private void OnValidate()
        {
            if (AssignValues())
                EditorUtility.SetDirty(this);
            GeneratedIds.Request();
        }

        // New entries (value 0) and duplicated list items get a value derived from the name, never one that is in use
        // or recorded as removed in the generated ledger.
        private bool AssignValues()
        {
            var taken = new HashSet<int> { 0 };
            foreach (string key in GeneratedIds.RecordedKeys(OutputName))
                if (int.TryParse(key, out int recorded))
                    taken.Add(recorded);
            var claimed = new HashSet<int>();
            bool changed = false;
            foreach (var entry in entries)
            {
                if (entry.Value != 0 && claimed.Add(entry.Value))
                    continue;
                int value = Target?.InitialValue(entry.Name) ?? Hash(entry.Name);
                while (value == 0 || taken.Contains(value) || claimed.Contains(value))
                    value = unchecked(value + 1);
                entry.Value = value;
                claimed.Add(value);
                changed = true;
            }
            return changed;
        }

        /// <summary>A positive 31-bit FNV-1a hash of a name; the default first value of a new entry.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in text ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
