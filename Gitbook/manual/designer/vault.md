# Vault: Typed Definitions

**Vault** holds the game's typed definitions. A Vault asset is an authored definition (an enemy's stats, an item, a level's rules) that the game prepares into an immutable runtime value. Engineers create definition types; designers create and tune the assets, and can build **field sets** with no code at all. Definitions are part of the Threadlink core; code reads them with `Vault.Get`.

## The Vault page

**Threadlink ▸ Workspace ▸ Vault** lists every definition in the project, grouped by type. Type in the filter to narrow it by name, type or folder, and select a definition to edit it beside the list. ⚠ marks a definition that needs an authoring migration.

- **New** creates a definition of any type that has a creation menu entry, beside the selected one (or in `Threadlink User/Design/Definitions`).
- Right-click a definition to ping, duplicate or delete it. A duplicate gets its own identity.
- To open a definition on the page, right-click it in the Project window and choose **Threadlink ▸ Open in Vault Page**, or use **Open in Vault Page** in its Inspector's context menu.

Edits made in Play mode change the asset and are kept after Play mode ends; the running game keeps the value it already prepared.

## Field sets

A **field set** is a definition made of named values, for data that does not need its own type: an enemy's damage and portrait, a weapon's range, a level's par time.

1. Create one with **Create ▸ Threadlink ▸ Vault ▸ Field Set**, or **New ▸ Threadlink ▸ Vault ▸ Field Set** on the Vault page.
2. Add a field, pick its ID from the dropdown and choose its kind: a number, text, true/false, a vector, color or rotation, or a reference to an asset such as a sprite or prefab.
3. Enter the value.

The field IDs come from an [ID domain](identifiers.md#your-own-id-domains) that extends **Vault fields**: add an entry there to make a new field available. Each field may appear once in a set. To make a family of similar sets, set a **Parent**: the set inherits every field of its parent, and fields you add replace the inherited ones of the same ID. **Create Typed Variant** in the Inspector makes such a child.

## Creating typed definitions

Use the asset creation menu entry the engineering team added for the type (for example **Create ▸ Game ▸ Enemy Definition**), or **New** on the Vault page. A new asset receives a permanent identity and the type's current schema version. Moving or renaming the asset keeps its identity.

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
