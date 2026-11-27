# Localizing Text

Threadlink shows player-visible text through Unity Localization (`com.unity.localization`). Text fields are `LocalizedString` references: a string table and an entry in it. Each field follows the selected language, and the Inspector edits it with Localization's table and entry picker.

## What is localized

| Text | Where it is authored |
|---|---|
| Interaction prompts | The **Prompt** field of every `Interactable` (Dextra) |
| Starter level names | `LevelDefinition` ▸ **Display Name** |
| Starter collectible names | `StarterCollectible` ▸ **Display Name**, inserted into its prompt as `{item}` |
| Starter messages, slot labels, settings and control names | `StarterContent` ▸ **Text**, and its string table |
| Starter screen labels and buttons | Bindings in the screens' UXML documents |
| Showcase demo text | The **Threadlink Showcase** string table |

Input prompts name controls and devices from the Input System (`Space`, `Button South`), or show the team's glyphs; they are not table entries. See [Input Prompts and Rebinding](input-prompts.md).

## Setup

Open **Threadlink ▸ Workspace ▸ Localization** and select **Set up**. It creates what is missing and is safe to run again:

- Localization settings in the engineering configs folder and the English (`en`) locale in the design configs folder (see [Configuration assets and folders](../engineer/editor-tooling.md#configuration-assets-and-folders)).
- Registration of every string table collection for loading. Localization loads tables through Addressables.

Importing the Starter or Showcase sample runs it automatically, because each sample brings an English string table collection: **Threadlink Starter** and **Threadlink Showcase**, under the sample's `Localization` folder. The Starter's validator reports a project that is not set up.

## Adding a language

1. On the Workspace's **Localization** page, pick the language in **Add locale** and select **Add**. The locale is created in the Locales folder, and every string table collection gets a table for it.
2. Translate the entries: **Open** a string table, or use **Tables window**.
3. Choose how the game selects the language in **Project Settings ▸ Localization** (locale selectors).

**New string table** creates a collection with a table for every locale in the String Tables folder. **Folder…** on the Locales and String Tables rows chooses where new ones go.

Entries are matched by key, so translations never change the assets that reference them.

## Writing entries

- Give each piece of text its own entry. Do not build sentences from fragments; word order differs between languages.
- Entries with `{0}`, `{1}` take values from the game, such as a slot number. Keep the placeholders in every translation.
- Smart entries take named values. The Starter's `take-item` entry is `Take {item}`, where `{item}` is the collectible's own localized name.
- A prompt with no table or entry shows nothing. The Starter's validator reports unassigned **Text** entries and level names.
