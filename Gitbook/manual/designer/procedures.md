# Designer Procedures

## Making an asset loadable

1. Make the asset addressable in the Addressables Groups window.
2. Open **Threadlink ▸ Content Mapping**, tick the entry and press **Apply**.
3. Tell engineering the ID, such as `ContentAssets.Characters.HeroPortrait`.

## Retiring content

1. Untick the entry in the Content mapping and press **Apply**. The ID becomes a tombstone, and code still using it produces a warning.
2. Remove the asset from Addressables only after engineering has updated the code.

## Tuning a definition

1. Select the definition asset or the variant you want to change.
2. In a variant, enable the override for each value you change; leave the others inherited.
3. Select **Validate Vault Asset** in the Inspector to check it.

## Adding a sound

1. Create an audio cue with **Create ▸ Threadlink ▸ Aura ▸ Audio Cue**.
2. Add clip variations, set the category and mixer group, and choose a voice limit and overflow policy.
3. Assign it where a component expects a cue: **Scene Music** for a scene's music, **Screen Sounds** for a screen, or a game component.

## Checking the project

Open **Threadlink ▸ Workspace** and select **Validation**. Errors identify assets the game would reject at runtime; warnings identify likely problems.

## Operating principles

- Identities are permanent: rename freely, but never reuse an identity for something else.
- Keep `.meta` files and identity ledgers with their assets.
- A failed validation is information, not an obstacle to hide: fix the asset rather than the check.
