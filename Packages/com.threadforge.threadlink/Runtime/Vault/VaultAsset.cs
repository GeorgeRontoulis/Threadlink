namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>Engineering-defined Unity authoring with explicit schema identity/version and immutable runtime mapping.</summary>
    public abstract class VaultAsset<T> : VaultAuthoring, IVaultSource<T>
    {
        [SerializeField, HideInInspector]
        private string identity;

        [SerializeField, HideInInspector]
        private int authoredVersion = 1;
        public override Guid Identity
        {
            get
            {
                if (Guid.TryParse(identity, out var value) && value != Guid.Empty)
                    return value;
#if UNITY_EDITOR
                if (UnityEditor.AssetDatabase.Contains(this))
                    throw new InvalidOperationException("Definition identity is missing. Select the asset so the Inspector assigns one.");
#endif
                // A definition created at runtime is identified for its lifetime only.
                identity = Guid.NewGuid().ToString("D");
                authoredVersion = SchemaVersion;
                return new Guid(identity);
            }
        }
        public abstract override Guid Schema { get; }
        public abstract override int SchemaVersion { get; }
        public override int AuthoredVersion => authoredVersion;
        public override VaultAuthoring ParentSource => null;

        public override void ValidateDefinition() => new VaultContext().Prepare(this);

        /// <summary>Maps serialized/nested/polymorphic authoring into an immutable DTO. No runtime reflection is required.</summary>
        protected abstract T BuildRuntime(VaultContext context);

        public T Build(VaultContext context)
        {
            if (authoredVersion != SchemaVersion)
                throw new InvalidOperationException("Explicit authoring migration is required.");
            ValidateAuthoring();
            return BuildRuntime(context);
        }

        protected virtual void ValidateAuthoring() { }

        /// <summary>Game-defined explicit conversion. Implement old-version readers rather than silently reinterpreting data.</summary>
        protected virtual void MigrateAuthoring(int sourceVersion) =>
            throw new NotSupportedException("No authoring migration is registered.");

        /// <summary>Runs an explicit migration in authoring tools; version commits only after successful conversion/validation.</summary>
        public override void ApplyAuthoringMigration()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                throw new InvalidOperationException("Authoring migrations run in Edit mode only.");
            if (authoredVersion == SchemaVersion)
                return;
            MigrateAuthoring(authoredVersion);
            ValidateAuthoring();
            authoredVersion = SchemaVersion;
#else
            throw new NotSupportedException(
                "Migrate authored definitions in the Editor before building."
            );
#endif
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(identity))
            {
                identity = Guid.NewGuid().ToString("D");
                authoredVersion = SchemaVersion;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    /// <summary>Explicit typed variant composition; no behavior graph or implicit gameplay system is supplied.</summary>
    public abstract class VaultVariant<T> : VaultAsset<T>
    {
        [SerializeField]
        [Tooltip("The Vault asset this variant inherits from. Enabled overrides replace its values.")]
        private VaultAsset<T> parent;
        public VaultAsset<T> Parent
        {
            get => parent;
            set => parent = value;
        }
        public override VaultAuthoring ParentSource => parent;

        /// <summary>Builds the root default if no parent is selected.</summary>
        protected abstract T BuildDefault(VaultContext context);

        /// <summary>Applies only enabled overrides. Construct a new immutable representation.</summary>
        protected abstract T ApplyOverrides(T inherited, VaultContext context);

        protected sealed override T BuildRuntime(VaultContext context)
        {
            if (parent == null)
                return ApplyOverrides(BuildDefault(context), context);
            if (parent.Schema != Schema || parent.SchemaVersion != SchemaVersion)
                throw new InvalidOperationException(
                    "Variant schema is incompatible; explicit migration is required."
                );
            return ApplyOverrides(context.Prepare(parent).Value, context);
        }

        /// <summary>Reports the immediate inherited origin for inspector/user tools.</summary>
        public Guid InheritedOrigin => parent == null ? Identity : parent.Identity;
    }
}
