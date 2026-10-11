# The Vault

A **Vault** is a polymorphic data asset holding a set of named, typed **fields**: the authoring unit for any game entity, such as an enemy, a weapon, a level or an item.

## Creation

1. In the Project window, select **Create ▸ Threadlink ▸ Vault**.
2. Assign a descriptive name (`Vault_Enemy_Goblin`, `Vault_Weapon_Longsword`).
3. Select the asset to expose its data-field map in the Inspector.

## Field composition

Each field comprises two elements:

1. A **field identifier**, selected from the Vault Fields dropdown (declared in `Vault.Fields.User.txt`, see [The Identifier Workflow](identifiers.md)).
2. A **typed value**, whose type is selected from a dropdown prior to entry.

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

Additional field types are introduced through a minor engineering task (see [Vault Runtime API](../engineer/vault.md#custom-field-types)).

## Serialised and transient backings

Each field is assigned one of two value backings:

- **Serialised**: persisted with the asset. Appropriate for authored values such as base health or weapon damage. This is the backing for design data.
- **Transient**: runtime-only, never persisted, reset each session. Appropriate for scratch values populated during play.

Authored values are serialised; runtime scratch values are transient.

> Unity [Asset Presets](https://docs.unity3d.com/Manual/Presets.html) may be applied to guarantee that every Vault of a given class is instantiated with its complete field set.

## Runtime consumption

Engineering reads and writes fields by identifier at runtime. Renaming a field identifier invalidates those references; engineering must be notified.

A Timeline can carry Vault values on markers (see [Vault Runtime API](../engineer/vault.md#timeline-integration)), and an animation event can pass a Vault to code through an `AnimationEventsReceiver` (see [Collections and Utilities](../engineer/collections-and-utilities.md#animation-helpers)).
