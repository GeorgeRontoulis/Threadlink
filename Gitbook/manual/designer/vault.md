# Vault: Typed Definitions

**Vault** holds the game's typed definitions. A Vault asset is an authored definition (an enemy's stats, an item, a level's rules) that the game prepares into an immutable runtime value. Engineers create the definition types; designers create and tune the assets. Definitions are part of the Threadlink core; code reads them with `Vault.Get`.

## Creating definitions

Use the asset creation menu entry the engineering team added for the type (for example **Create ▸ Game ▸ Enemy Definition**). A new asset receives a permanent identity and the type's current schema version. Moving or renaming the asset keeps its identity.

The Inspector shows the identity, schema and authored version. **Validate Vault Asset** checks the asset; a definition that fails validation also fails explicitly when the game prepares it, never silently.

## Variants and overrides

**Create Typed Variant** in the Inspector makes a child asset of the same type. A variant inherits every value from its parent and changes only the fields whose override is enabled:

- Enable an override to replace the inherited value.
- **Inherit** (shown per field) disables that override again; the parent's value applies.
- The Inspector shows where each value comes from along the parent chain.

Variants can have variants. A variant whose parent changes schema becomes invalid until it is migrated with the parent.

## Authoring migrations

When engineers change a definition type, they raise its schema version and write a conversion. Older assets then show a version mismatch. Select them and use **Apply Explicit Authoring Migration** in Edit mode; the Inspector records Undo and restores the asset if conversion fails. Builds never upgrade definitions silently, so migrate every listed asset before building.

Definition versions are separate from save-file migrations: changing a definition does not change saved games.
