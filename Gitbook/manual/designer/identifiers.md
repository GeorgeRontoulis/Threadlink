# Identifiers and Tombstones

Saves, replays and generated code refer to content by identity, not by name or path. An identity is assigned once and never reused, so renaming or moving an asset never breaks existing data.

## Content identities

Every entry ticked in the [Content mapping](registering-content.md) receives a value derived from its asset's GUID when it is first mapped.

- **Renaming, moving or regrouping** an asset keeps the value, so fields and saves that refer to it keep working; only the generated member's name and group follow the asset.
- **Unticking** an entry leaves its ID in the generated code as a **tombstone**, marked obsolete; code that still uses it produces a compiler warning, and loading it fails. The `.ids.json` ledger beside the generated class remembers it, and its value is never handed to other content.
- Never edit generated IDs, the ledger or the mapping in Project Settings by hand. Keep the ledger, the Content settings asset and `.meta` files under version control.

## Generated IDs

Scenes, input actions, audio categories and platform data (achievements, statistics, leaderboards, products) also have generated IDs. Scenes and input actions keep the identity Unity gives them (the scene asset's GUID, the action's ID), so renaming them keeps every reference working. Audio categories and platform data are identified by their names in Project Settings (platform data on each platform's page): renaming one removes the old ID. A removed ID stays in the generated code as an obsolete tombstone, recorded in the `.ids.json` ledger beside the class, until the code that uses it is updated. Fields that pick one of these, such as a level's scene or a cue's category, show a dropdown of what exists.

## Your own ID domains

When the game needs a fixed list of names, such as enemy kinds, spawn points or quest flags, ask for (or create) an **ID domain**: **Create ▸ Threadlink ▸ ID Domain**, or **Threadlink ▸ Workspace ▸ Code Generation ▸ New domain**. Add entries in the Inspector, with an optional description each. Engineers use the generated type in code, and fields of that type become dropdowns in the Inspector.

- An entry's value is fixed when you add it; renaming it keeps the value, so anything that already uses it keeps working.
- Removing an entry keeps it as an obsolete tombstone in the code until engineers update the code that uses it. Its value is never reused.
- Set **Owner** to **Design** for lists designers maintain; the asset then lives in `Design/Identifiers`.

## Deterministic domains

A StatelessRNG domain separates the random numbers of one system from another. Engineers declare domains in code as `StatelessRNG.Domain` values, either with an explicit number or from a name. A domain's value is part of the save and replay format: changing it changes every number drawn under it. Rename a domain's code accessor freely, but keep its value.
