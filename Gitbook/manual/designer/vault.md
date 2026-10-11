# The Vault

A **Vault** is a data asset holding named, typed **fields**. Use one per game entity, such as an enemy, a weapon, a level or an item.

## Creation

1. In the Project window, select **Create ▸ Threadlink ▸ Vault**, or select **+** in the Vault Browser.
2. Give it a descriptive name (`Vault_Enemy_Goblin`, `Vault_Weapon_Longsword`).
3. Select it to edit its fields in the Inspector.

## The Vault Browser

**`Threadlink ▸ Vault Browser`** lists every Vault in the project, with its folder and field count. Select one to edit it in place, with undo. Search filters by name or folder, and right-clicking a Vault offers ping, duplicate and delete. To jump to a Vault in the browser, right-click it in the Project window and choose **Threadlink ▸ Open in Vault Browser**.

Edits made in Play Mode change the asset itself and are kept after Play Mode ends.

## Field composition

Each field has:

1. A **field identifier**, picked from the Vault Fields dropdown (declared in `Vault.Fields.User.txt`; see [The Identifier Workflow](identifiers.md)).
2. A **value**, whose type you pick from a dropdown before entering it.

Available field types:

| Type | Representation |
|---|---|
| `Integer` | 32-bit integer |
| `Float` | Single-precision floating point |
| `Boolean` | Boolean |
| `Double` | Double-precision floating point |
| `Long` | 64-bit integer |
| `Integer2D` | Integer pair (`int2`) |
| `Float2D` | Float pair (`float2`) |
| `Vector2D` | Two-component vector |
| `Vector3D` | Three-component vector |
| `Rotation` | Quaternion |
| `UnityGameObject` | GameObject reference |
| `LocalizedText` | Localised string; requires the Unity Localization package |

Engineers can add more field types (see [Vault Runtime API](../engineer/vault.md#custom-field-types)).

## Serialised and transient backings

Each field stores its value in one of two ways:

- **Serialised**: saved with the asset. Use it for authored values such as base health or weapon damage.
- **Transient**: runtime-only, never saved, reset each session. Use it for scratch values set during play.

> Unity [Presets](https://docs.unity3d.com/Manual/Presets.html) can give every new Vault of a kind its full set of fields.

## Runtime consumption

Engineers read and write fields by identifier at runtime. Renaming a field identifier breaks those references, so tell engineering first.

A Timeline can carry Vault values on markers (see [Vault Runtime API](../engineer/vault.md#timeline-integration)), and an animation event can pass a Vault to code through an `AnimationEventsReceiver` (see [Collections and Utilities](../engineer/collections-and-utilities.md#animation-helpers)).
