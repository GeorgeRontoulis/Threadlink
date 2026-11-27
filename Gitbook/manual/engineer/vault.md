# Vault Runtime API

Vault turns designer-edited assets into immutable values that game code reads. It is part of the core. Designers' side of it is described in [Vault: Typed Definitions](../designer/vault.md).

## Defining a type

A definition has two halves: an immutable **runtime type** that game code reads, and an **authoring class** that designers edit. The authoring class derives from `VaultAsset<T>`, declares a schema and its version, and builds the runtime value from its fields:

```csharp
public sealed class EnemyStats
{
    public EnemyStats(string name, int health, float speed) => (Name, Health, Speed) = (name, health, speed);

    public string Name { get; }
    public int Health { get; }
    public float Speed { get; }
}

[CreateAssetMenu(menuName = "Game/Enemy Definition")]
public sealed class EnemyDefinition : VaultAsset<EnemyStats>
{
    public string DisplayName = "Slime";
    [Min(1)] public int Health = 30;
    public float Speed = 2;

    public override Guid Schema => new Guid("8a1f2c3d-4e5f-4061-8273-94a5b6c7d8e9");   // fixed forever
    public override int SchemaVersion => 1;                                             // raised when fields change

    protected override EnemyStats BuildRuntime(VaultContext context)
    {
        if (Health < 1)
            throw new InvalidOperationException("Health must be positive.");
        return new EnemyStats(DisplayName, Health, Speed);
    }
}
```

Game code reads the prepared value:

```csharp
EnemyStats stats = Vault.Get(enemyDefinition);
```

| API | Behavior |
| --- | --- |
| `Vault.Get(definition)` | The definition's runtime value. It is prepared once, on first use, and shared for the session. |
| `Vault.Prepare(definition)` | The same value as a `VaultRecord<T>`, with its identity, schema and version. |
| `BuildRuntime(context)` | Builds the runtime value. Throw to reject invalid data; preparation fails with your message. |
| `ValidateAuthoring()` | Optional checks that run before every preparation and after a migration. Throw to reject the asset. |
| `MigrateAuthoring(sourceVersion)` | Converts an asset authored with an older `SchemaVersion`; see below. |

The runtime type must be truly immutable: expose read-only state and copy lists and arrays instead of handing out the asset's own. A prepared value does not change when the asset is edited later. Keep state that changes during play, such as an enemy's current health, in separate objects.

Nested serializable data and `[SerializeReference]` polymorphism work as usual in authoring classes; Vault adds no reflection-based serializer.

## Preparation

Preparing a definition checks its schema and version, that its identity is set and unique, and that variants do not form a cycle. Any failure throws, naming the asset; nothing is silently skipped. A definition whose authored version differs from its type's `SchemaVersion` fails until it is migrated.

## Variants

A variant inherits its values from a parent asset of the same type and overrides some of them. Derive from `VaultVariant<T>`, store each overridable value in a `VaultOverride<T>` field, and implement two methods:

| Method | Returns |
| --- | --- |
| `BuildDefault(context)` | The value used when the variant has no parent. |
| `ApplyOverrides(inherited, context)` | A new value: the parent's, with each enabled override applied through `field.Resolve(inherited.Value)`. |

```csharp
public sealed class EnemyVariant : VaultVariant<EnemyStats>
{
    public VaultOverride<int> Health;

    public override Guid Schema => new Guid("8a1f2c3d-4e5f-4061-8273-94a5b6c7d8e9");   // the parent's schema
    public override int SchemaVersion => 1;

    protected override EnemyStats BuildDefault(VaultContext context) => new EnemyStats("Enemy", 10, 1);

    protected override EnemyStats ApplyOverrides(EnemyStats inherited, VaultContext context) =>
        new EnemyStats(inherited.Name, Health.Resolve(inherited.Health), inherited.Speed);
}
```

A variant must have its parent's schema and version. `VaultOverride<T>.Resolve` returns the inherited value while the override is disabled; `Reset` disables it again. The Inspector shows where each value comes from along the parent chain, and **Inherit** disables one override.

## Authoring versions

A new asset gets a permanent identity (a GUID) and its type's current `SchemaVersion`; moving or renaming it keeps both. When you change an authoring class's fields:

1. Raise `SchemaVersion`.
2. Override `MigrateAuthoring(sourceVersion)` to convert fields from each older version.
3. Have designers run **Apply Explicit Authoring Migration** on the listed assets in Edit mode (the Workspace's Vault page lists them). It records Undo and restores the asset if the conversion throws.

Players never upgrade definitions on their own, so migrate every asset before building. Authoring versions are unrelated to save-file versions ([Serialization](serialization.md#versions-and-migrations)).

## Vault fields

`VaultFieldSet` is an optional bag of named, immutable scalar values, for data that does not deserve its own field. Read it with generated keys: an [ID domain](editor-tooling.md#id-domains) that extends **Vault fields** generates `VaultFields.Name` (a `VaultField`), so `fields.Get<int>(VaultFields.Damage)` and `fields.TryGet(VaultFields.Damage, out int damage)` are checked by the compiler. A key is the entry's name in PascalCase.

`VaultContext`, `VaultRecord<T>`, `IVaultSource<T>`, `VaultOverride<T>` and `VaultFieldSet` are public for tools and tests.
