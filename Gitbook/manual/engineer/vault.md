# Vault Runtime API

Vault turns designer-edited assets into immutable values that game code reads. It is part of the core. Designers' side of it is described in [Vault: Typed Definitions](../designer/vault.md).

## Defining a type

A definition has two halves: an immutable **runtime type** that game code reads, and an **authoring class** that designers edit. In the short form you write only the authoring class's fields; Threadlink generates the rest:

```csharp
public sealed partial class EnemyStats { }   // filled in by Threadlink

[CreateAssetMenu(menuName = "Game/Enemy Definition")]
public sealed partial class EnemyDefinition : VaultAsset<EnemyStats>
{
    [Tooltip("Name shown in the bestiary.")]
    public string DisplayName = "Slime";
    [Min(1)] public int Health = 30;
    public float Speed = 2;
    public List<ItemDefinition> Drops = new List<ItemDefinition>();
}
```

Because both classes are `partial`, Threadlink generates:

- on `EnemyStats`, one read-only property per serialized field (`DisplayName`, `Health`, `Speed`, and `Drops` as an `IReadOnlyList<ItemDefinition>`), documented with the field's tooltip, and a constructor;
- on `EnemyDefinition`, `BuildRuntime`, which copies the fields into a new `EnemyStats`. Arrays and lists are copied, so the runtime value never changes with the asset.

Serialized fields are the class's own public fields and `[SerializeField]` private ones, without `[NonSerialized]`. A runtime type with members of its own is not generated: write its constructor with parameters named after its properties, and Threadlink generates `BuildRuntime` to call it, matching each parameter to the field of the same name. Write `BuildRuntime` yourself to validate, compute or reshape values; Threadlink then generates nothing for that class.

| Diagnostic | Meaning |
| --- | --- |
| TL0002 | The mapping cannot be generated, for example because no field matches a constructor parameter. The message names it. |
| TL0003 | The definition (or a class containing it) is not `partial`, so nothing is generated. |
| TL0004 | Several definition classes use one empty `partial` runtime type; give it members by hand, or use one definition class. |

`Schema` identifies the definition type and defaults to a value derived from the runtime type, so definitions and variants of one runtime type share it. `SchemaVersion` defaults to 1. Override `Schema` with a fixed GUID only to keep it when the runtime type is renamed, and raise `SchemaVersion` when the authored fields change (see [Authoring versions](#authoring-versions)). The explicit form writes everything by hand:

```csharp
public sealed class EnemyStats
{
    public EnemyStats(string name, int health) => (Name, Health) = (name, health);

    public string Name { get; }
    public int Health { get; }
}

[CreateAssetMenu(menuName = "Game/Enemy Definition")]
public sealed class EnemyDefinition : VaultAsset<EnemyStats>
{
    public string DisplayName = "Slime";
    [Min(1)] public int Health = 30;

    protected override EnemyStats BuildRuntime(VaultContext context)
    {
        if (Health < 1)
            throw new InvalidOperationException("Health must be positive.");
        return new EnemyStats(DisplayName, Health);
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

A variant inherits its values from a parent asset of the same runtime type and overrides some of them. In the short form, declare an empty `partial` class:

```csharp
[CreateAssetMenu(menuName = "Game/Enemy Variant")]
public sealed partial class EnemyVariant : VaultVariant<EnemyStats> { }
```

Threadlink generates a `VaultOverride<T>` field for each value of `EnemyStats`, `BuildDefault` (every override's value, used when the variant has no parent) and `ApplyOverrides` (the parent's value with every enabled override applied). A `VaultOverride<T>` field you declare yourself, named after a value, is used instead of a generated one; write either method yourself to replace the generated one.

By hand, store each overridable value in a `VaultOverride<T>` field and implement both methods:

| Method | Returns |
| --- | --- |
| `BuildDefault(context)` | The value used when the variant has no parent. |
| `ApplyOverrides(inherited, context)` | A new value: the parent's, with each enabled override applied through `field.Resolve(inherited.Value)`. |

```csharp
public sealed class EnemyVariant : VaultVariant<EnemyStats>
{
    public VaultOverride<int> Health;

    protected override EnemyStats BuildDefault(VaultContext context) => new EnemyStats("Enemy", 10);

    protected override EnemyStats ApplyOverrides(EnemyStats inherited, VaultContext context) =>
        new EnemyStats(inherited.Name, Health.Resolve(inherited.Health));
}
```

A variant must have its parent's schema and version. `VaultOverride<T>.Resolve` returns the inherited value while the override is disabled; `Reset` disables it again. The Inspector shows where each value comes from along the parent chain, and **Inherit** disables one override.

## Authoring versions

A new asset gets a permanent identity (a GUID) and its type's current `SchemaVersion`; moving or renaming it keeps both. When you change an authoring class's fields:

1. Raise `SchemaVersion`.
2. Override `MigrateAuthoring(sourceVersion)` to convert fields from each older version.
3. Have designers run **Apply Explicit Authoring Migration** on the listed assets in Edit mode (the Workspace's Vault page lists them). It records Undo and restores the asset if the conversion throws.

Players never upgrade definitions on their own, so migrate every asset before building. Authoring versions are unrelated to save-file versions ([Serialization](serialization.md#versions-and-migrations)).

## Field sets

`VaultFieldSetAsset` (**Create ▸ Threadlink ▸ Vault ▸ Field Set**) is a definition designers build without code: a list of fields, each a generated `VaultFields` key with a typed value. Its runtime value is a `VaultFieldSet`:

```csharp
VaultFieldSet goblin = Vault.Get(goblinFields);
int damage = goblin.Get<int>(VaultFields.Damage);
if (goblin.TryGet(VaultFields.Portrait, out Sprite portrait))
    icon.sprite = portrait;
```

| Kind | Read as |
| --- | --- |
| Integer, Long, Float, Double, Boolean, Text | `int`, `long`, `float`, `double`, `bool`, `string` |
| Vector2, Vector3, Vector4, Vector2Int, Vector3Int, Color | The Unity type of the same name |
| Rotation | `Quaternion` (authored as Euler angles) |
| Object | The referenced asset, such as a `Sprite`, `AudioCue` or prefab |

The keys are generated: an [ID domain](editor-tooling.md#id-domains) that extends **Vault fields** generates `VaultFields.Name` (a `VaultField`), so a misspelled field is a compile error. A key is the entry's name in PascalCase.

A set can name a **Parent** set: it inherits every parent field, and its own fields add to or replace them. A field may appear once per set. `Get` throws when the field is missing or holds another type; `TryGet` returns false; `Contains` and `Count` describe the set. The **Vault** validator warns about a field no ID domain defines. Tools and tests edit sets with `Set(field, value)` and `Remove(field)`.

`VaultContext`, `VaultRecord<T>`, `IVaultSource<T>` and `VaultOverride<T>` are public for tools and tests.
