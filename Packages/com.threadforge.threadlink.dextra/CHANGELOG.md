# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0]

### Added

- Workspace **Localization** page and `LocalizationSetup`: setup creates Localization settings and the English locale if missing and registers every string table collection for loading, and runs automatically when a Threadlink sample's string tables are imported. The page adds locales (with a table in every collection) and string tables, in their configured folders.
- Input configuration on the Workspace's Configuration page: Player Actions, created from the Input System's default actions with Dextra's map names, the Input System settings, and Screen Panel Settings with a theme. Creating one assigns it to Dextra's settings or the Input System.
- Dependency on Unity Localization 1.5.14.

### Changed

- `Interactable.Prompt`, `InteractionAvailability.Prompt` and `InteractionSensor.Prompt` are `LocalizedString` string table entries instead of strings. The sensor's prompt is null when nothing is selected. Assign the **Prompt** field of existing interactables to a table entry.

## [3.0.0]

### Changed

- `PlayerInputContext.Available` is `IsConnected`; `ExportOverrides`/`ImportOverrides` are `ExportBindings`/`ImportBindings`, matching `ResetBindings`.
- `InteractionAvailability.Available` is `IsAvailable`; `Interactor.Availability(actor)` is `Evaluate(actor)`, as on `IInteractable`.
- `ScreenHandle.Instance` is `ScreenHandle.GameObject`. `ScreenHandle` is created only by Dextra, and `DextraSettings.MapName` is internal.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Added

- Input events on Iris: `PlayerJoined`, `PlayerLeft`, `DeviceLost`, `DeviceRegained`, `ControlsChanged` and `BindingsChanged`.
- Screen events on Iris: `ScreenOpened`, `ScreenClosed` and `GameplayInputBlockChanged`.
- Player 0 uses devices connected after startup. Devices it uses implicitly move to a player they are assigned to.

### Changed

- Dextra boots on its own; screens close while it deinitializes, while their content is still available.

## [1.0.0]

First release.

### Added

- Dextra input, screens and interaction, with optional Aura screen sounds and uGUI screens.
