# Designer Procedures

## Declaring a spawn point

1. Add the identifier to `Threadlink User/Native Domain Injectors/Nexus.SpawnPoints.User.txt`.
2. Save. Generation runs automatically.
3. The spawn point becomes selectable wherever spawn points are configured.

## Adding a tunable value to an entity

1. Add the field identifier to `Vault.Fields.User.txt` and save.
2. Open the entity's Vault, add a field with that identifier, select its type, and enter the value.
3. Select the **Serialised** or **Transient** backing.

## Declaring an input context

1. Add the mode identifier to `Dextra.InputModes.User.txt` and save.
2. Provide the identifier to engineering for binding to an input action map.

## Making an asset loadable

1. Assign the asset to an Addressable group under a stable name.
2. Map it through **`Threadlink ▸ Addressables ▸ Mapping Window`** and select **Apply**.

## Placing an audio zone

1. Add an **AuraZone** and an **AudioSource** to a GameObject in the scene, and assign the source's clip.
2. Give the zone a name unique within the scene.
3. Set **Radius Coefficient** and **Influence**.

## Operating principles

- Nothing is referenced by raw string. Declare an identifier, save, and select from the dropdown.
- Removal is safe; renaming orphans existing references and requires coordination.
- Consult the console summary emitted by each generation pass.
- Assign stable names and final group membership to Addressable assets before mapping them.
- Notify engineering of any identifier rename that may be referenced in code.
