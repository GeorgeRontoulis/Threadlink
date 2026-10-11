# Menus and Asset Creation

## Threadlink menu

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Forces a generation pass across every domain. |
| `Threadlink ▸ Code Formatter` | Recursively formats C# files in a selected project folder using CSharpier. |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers. |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Registers framework assets with the Addressables system. |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Realigns addresses to asset paths. |
| `Threadlink ▸ Clear all Binaries` | Empties `.bytes` files within a selected directory. |
| `Threadlink ▸ Registers Tracker` | Inspects live register contents. |

The **Scenes** dropdown in the main toolbar opens any scene that ships (see [Editor Tooling](../engineer/editor-tooling.md#scene-dropdown)).

## Asset creation

| Entry | Creates |
|---|---|
| `Create ▸ Threadlink ▸ Native Config` | `ThreadlinkConfig.Native.asset` |
| `Create ▸ Threadlink ▸ User Config` | `ThreadlinkConfig.User.asset` |
| `Create ▸ Threadlink ▸ Editor Config` | `ThreadlinkConfig.Editor.asset` |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | A Chronos Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Aura Config` | An Aura Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Dextra Config` | A Dextra Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Sentinel Config` | `SentinelConfig.asset` |
| `Create ▸ Threadlink ▸ Vault` | A Vault |
| `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config` | An Interactable Config |
| `Create ▸ Threadlink ▸ Animation ▸ Animator Hash` | An Animator Hash |
