# Registering Content

Content the game loads at runtime is managed by **Addressables**: its Groups window decides which assets are addressable, their addresses, labels, groups and how they are built and delivered. Threadlink does not duplicate any of that. The **Content mapping** only decides which addressable entries game code loads by ID.

## Map an entry

1. Make the asset addressable in **Window ▸ Asset Management ▸ Addressables ▸ Groups** (or tick **Addressable** in its Inspector).
2. Open **Threadlink ▸ Content Mapping** (the Workspace's **Content** page). Every editable Addressables group is listed with its entries.
3. Tick the entry, then **Apply**. **All** and **None** tick every shown entry of a group; the filter narrows the list by name.
4. Tell engineering the ID: a prefab `Slime` in the group `Characters` becomes `ContentPrefabs.Characters.Slime`.

Threadlink sorts each entry by how it loads:

| Kind | Entries | Generated class |
|---|---|---|
| Asset | Sprites, clips, cues, materials, any other asset | `ContentAssets` |
| Prefab | Prefabs | `ContentPrefabs` |
| Scene | Scenes | `ContentScenes` |

You can also select assets in the Project window and choose **Assets ▸ Threadlink ▸ Map for Loading by ID**. Fields that refer to content, such as a level's music, show a dropdown of the mapped entries.

The mapping itself is in **Project Settings ▸ Threadlink ▸ Content**: three read-only dictionaries of IDs and Addressables references, written when you apply.

## Labels

Addressables labels need no mapping: each label generates `ContentLabels.Name`, which loads every asset with that label in one request.

Removing or renaming content follows the rules in [Identifiers and Tombstones](identifiers.md).
