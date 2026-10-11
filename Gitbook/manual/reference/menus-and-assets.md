# Menus and Asset Creation

## Threadlink menu

| Command | Function |
|---|---|
| `Threadlink ▸ Vault Browser` | Lists every Vault, and inspects and edits the selected one (see [Editor Tooling](../engineer/editor-tooling.md#vault-browser)). |
| `Threadlink ▸ Runtime Monitor` | Shows the live runtime as a graph, with per-subsystem state and frame, memory and rendering metrics (see [Editor Tooling](../engineer/editor-tooling.md#runtime-monitor)). |
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Runs a generation pass for every domain. |
| `Threadlink ▸ Code Formatter` | Formats the C# files in a chosen folder with CSharpier. |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers. |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Makes the framework's own assets Addressable. |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Sets each address to its asset path. |
| `Threadlink ▸ Clear all Binaries` | Empties the `.bytes` files in a chosen folder. |

A Vault's context menus also offer **Open in Vault Browser**: `Assets ▸ Threadlink` in the Project window, and the Inspector's context menu.

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
