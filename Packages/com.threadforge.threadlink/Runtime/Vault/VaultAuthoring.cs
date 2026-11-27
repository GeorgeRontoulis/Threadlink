namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>Non-generic authoring metadata for validation, migration and provenance tools.</summary>
    public abstract class VaultAuthoring : ScriptableObject
    {
        public abstract Guid Identity { get; }
        public abstract Guid Schema { get; }
        public abstract int SchemaVersion { get; }
        public abstract int AuthoredVersion { get; }
        public abstract VaultAuthoring ParentSource { get; }
        public abstract void ValidateDefinition();
        public abstract void ApplyAuthoringMigration();
    }
}
