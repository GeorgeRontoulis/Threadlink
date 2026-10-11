# Registering Scenes, Prefabs and Assets

Runtime-loaded content (scenes, prefabs, audio clips, sprites) is referenced through Addressables and surfaced as a dropdown identifier. Registration is performed through a dedicated tool.

Every scene is Addressable except the bootstrap scene, which is the only one in Build Settings (see [Deployment Overview](../foundations/deployment-overview.md)). A new scene therefore never goes into Build Settings.

1. Ensure the asset belongs to an Addressable group.
2. Open **`Threadlink ▸ Addressables ▸ Mapping Window`**.
3. Locate the asset within its group and enable its checkbox. The toolbar's search field filters entries by name; **All** and **None** toggle a whole group; **Ping** locates the asset in the Project window.
4. Select **Apply**.

The window classifies assets by type automatically (scenes yield Scene identifiers, prefabs yield Prefab identifiers, all others yield Asset identifiers) and writes the corresponding reference into the User Config. The reference maps on the User Config are read-only in the Inspector and are owned exclusively by this window. Read-only Addressable groups are not listed.

Two properties govern the resulting identifier:

- **The identifier derives from the asset name.** An audio clip named `Music_BossTheme` yields the identifier `Music_BossTheme`. Assets should carry stable, code-safe names prior to mapping.
- **The Addressable group participates in identity.** Two assets named `Splash` in distinct groups coexist; the second is qualified as `GroupName_Splash`. Relocating a mapped asset to a different group alters its identifier. Group assignment should be settled before mapping.

Disabling an asset's checkbox unmaps it. The identifier is retained as a tombstone.

Renaming a mapped asset, or moving it to another group, gives it a new identifier and makes the old one a tombstone. Applying the mappings again records the old identifier as an alias of the new one, so engineering can resolve data saved under the old identifier (see [Resource Loading](../engineer/resource-loading.md#renamed-assets)). References in assets and code still require reassignment.

Group names become part of the identity too: two groups whose names reduce to the same code-safe form (for example `Test Assets` and `Test_Assets`) are reported, and Apply refuses to run until one is renamed.

The **Scenes** dropdown in the Editor's main toolbar lists every scene that ships, both those in Build Settings and those in Addressable groups, and opens the selected one.
