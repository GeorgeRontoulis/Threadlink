# Vault Runtime API

`Vault` (namespace `Threadlink.Vault`) is a `LinkableAsset`. Its fields are read and written by identifier:

```csharp
vault.Has(fieldID);
vault.TryGetDataField(fieldID, out DataField field);
vault.TryGetGenericDataField<float>(fieldID, out DataField<float> field);
vault.TryGetConcreteDataField<Float>(fieldID, out Float field);
vault.TryGet<float>(fieldID, out float value);
vault.TrySet<float>(fieldID, value);
```

`TryGet<T>` fails when the field is absent, is not a `DataField<T>`, or holds `null`. `TrySet<T>` fails when the field is absent or is not a `DataField<T>`. Every member is virtual, so a derived Vault can intercept access.

Fields are stored in a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`, backed by `[SerializeReference]` to permit polymorphic field types. The shipped field types reside in `Threadlink.Vault.DataFields` and are listed in [The Vault](../designer/vault.md#field-composition).

`DataField<T>` exposes `Value` and an `OnValueChanged` event, raised on every assignment. The backing is either `SerializedValue<T>` or `TransientValue<T>`; the transient variant is `[NonSerialized]` and resets per session. As a Vault is a ScriptableObject, transient state persists across editor play-mode sessions unless explicitly reset. Discarding a field clears its backing and raises `OnValueChanged` with `default` before removing its listeners. `TryApplyValueTo(vault, fieldID)` copies a field's value into another Vault's field of the same type.

## Custom field types

A new field type is a serialisable subclass of `DataField<T>` in a runtime assembly:

```csharp
[Serializable] public sealed class Colour : DataField<Color> { }
```

It then appears in the Inspector's field-type dropdown.

## Timeline integration

Under `THREADLINK_TIMELINE`, a Timeline can carry Vault values:

- **`VaultMarker`** is a Timeline marker holding a list of field identifiers with typed values, and notification flags.
- **`VaultTrack`** is a marker track bound to a `VaultReceiver`.
- **`VaultReceiver`** is a component that raises `OnMarkerReceived(PlayableDirector, VaultMarker)` when a marker fires. `ClearCallback()` removes its listeners.

Applying the values is the listener's decision: `marker.TryApplyConfigTo(vault)` writes every value of the marker into the given Vault, logging each field it could not set.
