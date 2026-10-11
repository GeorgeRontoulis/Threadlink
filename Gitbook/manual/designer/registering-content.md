# Registering Scenes, Prefabs and Assets

Content loaded at runtime (scenes, prefabs, audio clips, sprites) is referenced through Addressables and picked from a dropdown. The Mapping Window adds it to the dropdowns.

Every scene is Addressable except the bootstrap scene, which is the only one in Build Settings (see [Deployment Overview](../foundations/deployment-overview.md)). So never add a new scene to Build Settings.

1. Put the asset in an Addressable group.
2. Open **`Threadlink ▸ Addressables ▸ Mapping Window`**.
3. Find the asset in its group and tick it. The search field filters by name; **All** and **None** tick or clear a whole group; **Ping** shows the asset in the Project window.
4. Select **Apply**.

The window sorts assets by type (scenes become Scene identifiers, prefabs Prefab identifiers, everything else Asset identifiers) and writes the references into the User Config. Only this window edits those references; they are read-only in the Inspector. Read-only Addressable groups are not listed.

Two things decide the identifier:

- **The asset's name.** An audio clip named `Music_BossTheme` becomes `Music_BossTheme`. Give assets stable, code-safe names before mapping them.
- **The asset's group.** Two assets named `Splash` in different groups can coexist; the second becomes `GroupName_Splash`. Moving a mapped asset to another group changes its identifier, so settle groups before mapping.

Clearing an asset's tick unmaps it; its identifier stays as a tombstone.

Renaming a mapped asset, or moving it to another group, gives it a new identifier and leaves the old one as a tombstone. The next Apply records the old identifier as an alias of the new one, so data saved under the old identifier still resolves (see [Resource Loading](../engineer/resource-loading.md#renamed-assets)). References in assets and code still need reassigning.

Two groups whose names reduce to the same code-safe form (for example `Test Assets` and `Test_Assets`) are reported, and Apply won't run until one is renamed.

The **Scenes** dropdown in the main toolbar lists every scene that ships, from Build Settings and from Addressable groups, and opens the one you pick.
