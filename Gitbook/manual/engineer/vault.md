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

`TryGet<T>` fails if the field is missing, isn't a `DataField<T>`, or holds `null`. `TrySet<T>` fails if the field is missing or isn't a `DataField<T>`. Every member is virtual, so a derived Vault can intercept access.

Fields live in a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`, which uses `[SerializeReference]` so fields can be of any type. The shipped field types are in `Threadlink.Vault.DataFields` and listed in [The Vault](../designer/vault.md#field-composition).

`DataField<T>` has a `Value` and an `OnValueChanged` event, raised on every assignment. Its backing is `SerializedValue<T>` or `TransientValue<T>`. The transient one is `[NonSerialized]` and resets each session, but because a Vault is a ScriptableObject, it survives between Play sessions in the Editor unless you reset it. Discarding a field clears its backing and raises `OnValueChanged` with `default`, then removes its listeners. `TryApplyValueTo(vault, fieldID)` copies a field's value into another Vault's field of the same type.

The [Vault Browser](editor-tooling.md#vault-browser) lists and edits every Vault in the project.

## Custom field types

To add a field type, write a serialisable subclass of `DataField<T>` in a runtime assembly:

```csharp
[Serializable] public sealed class Colour : DataField<Color> { }
```

It appears in the field-type dropdown.

## Timeline integration

Under `THREADLINK_TIMELINE`, a Timeline can carry Vault values:

- **`VaultMarker`** is a Timeline marker holding field identifiers with typed values, and notification flags.
- **`VaultTrack`** is a marker track bound to a `VaultReceiver`.
- **`VaultReceiver`** is a component that raises `OnMarkerReceived(PlayableDirector, VaultMarker)` when a marker fires. `ClearCallback()` removes its listeners.

The listener decides what to do with the values. `marker.TryApplyConfigTo(vault)` writes them all into a Vault, logging any field it couldn't set.
