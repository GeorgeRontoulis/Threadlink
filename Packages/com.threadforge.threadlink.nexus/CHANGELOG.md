# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0]

### Changed

- `TransitionOutcome` is internal; `Nexus.AddScenes`, called by generated code, is hidden from IntelliSense.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Added

- `SceneTransitionStarted` and `SceneTransitionFailed` on Iris, alongside `SceneChanged`.

### Changed

- Nexus boots on its own and unloads its scenes while deinitializing, while their content is still available.

## [1.0.0]

First release.

### Added

- Nexus scene transitions, holds and transition steps for Build Profile and Addressables scenes.
