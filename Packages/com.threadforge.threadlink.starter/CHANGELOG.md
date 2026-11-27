# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0]

### Added

- The **Threadlink Starter** English string table. Screen labels and buttons bind to it in UXML, and `StarterContent.Text` points at the entries the game fills in: slot labels, save messages, recovery, and control and volume names.
- The Starter validator and Workspace page report a project without Localization set up and link to the Workspace's Localization page.
- Dependency on Unity Localization 1.5.14.

### Changed

- `LevelDefinition.DisplayName` and `PreparedLevel.DisplayName` are `LocalizedString`. Level definitions migrate to schema version 2; set **Display Name** to a table entry.
- `StarterCollectible`'s display name is a `LocalizedString`, passed to its prompt as the smart variable `{item}`. `StarterLevel.Prompt` is a `LocalizedString`.
- `StarterExit` uses its **Prompt** field.
- Saves record the level by its identity.

## [3.0.0]

No changes; released with the framework's shared version.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Changed

- `StarterGame` no longer declares a startup order: every subsystem is available when it is used.

## [1.0.0]

First release.

### Added

- A small complete game: menu and forest scenes, three save slots with recovery, settings with saved rebinding, music and screen sounds, built as one `StarterGame` subsystem with designer data in `StarterContent`.
