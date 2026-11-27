# Vault Runtime API

## Typed definitions

Vault is part of the core. Its types share the codename: authoring classes derive from `VaultAsset<T>` or `VaultVariant<T>`, and preparation uses `VaultContext`, `VaultRecord<T>`, `IVaultSource<T>`, `VaultOverride<T>` and `VaultFieldSet`. `Vault.Get(definition)` returns a definition's prepared, immutable value, prepared once and shared for the session; `Vault.Prepare(definition)` returns it with its identity, schema and version. `VaultContext`, `VaultRecord<T>`, `IVaultSource<T>`, `VaultOverride<T>` and the field containers are available for tools and tests.

Derive an authoring class from `VaultAsset<T>` or `VaultVariant<T>`. Declare stable `Schema` and `SchemaVersion` values and explicitly map serialized fields to `T`. The runtime type must expose read-only state and copy mutable collections; returning a mutable asset or list does not become immutable automatically. Nested serialized data and `[SerializeReference]` polymorphism remain native authoring features. No runtime reflection serializer or behavior graph is supplied.

```csharp
EnemyStats stats = Vault.Get(enemyDefinition);
```

Preparation validates schema/version, stable identity, duplicate identities and recursive cycles. Variants prepare their typed parent through the same context, then apply enabled overrides to construct a new runtime value. Parent schemas and versions must match. `VaultOverride<T>.Resolve` inherits when disabled; `Reset` restores inheritance. A prepared result remains independent of later authoring changes when its mapping copies data correctly. Mutable gameplay state belongs in separate objects.

New assets receive a persistent authoring GUID and current schema version. Keep that identity through moves and migrations. Existing version mismatches fail explicitly. Implement old-version conversion in `MigrateAuthoring`; run **Apply Explicit Authoring Migration** in Edit mode before building. The Inspector records Undo and restores on failure. Player preparation never silently upgrades authored data. These definition versions are separate from persistence save-schema migrations and identifier tombstones.

The UI Toolkit Inspector displays identity, schema and authored version, validates the mapping, creates a typed child variant, and reports each `VaultOverride` origin through its parent chain. **Inherit** disables only that override. Package samples include typed nested/polymorphic mapping; consumers may define additional engineering-controlled authoring types.

## Vault fields

`VaultFieldSet` is an opt-in bag of immutable scalar values keyed by name. Read it with generated keys instead of strings: an [ID domain](editor-tooling.md#id-domains) that extends **Vault fields** generates `VaultFields.Name` (a `VaultField`), so `fields.Get<int>(VaultFields.Damage)` and `fields.TryGet(VaultFields.Damage, out int damage)` are checked by the compiler. A field key is the entry's name in PascalCase.
