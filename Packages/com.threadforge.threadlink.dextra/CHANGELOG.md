# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
