# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0]

### Added

- `MusicChanged`, `VolumeChanged` and `MuteChanged` on Iris.

### Changed

- Aura and scene music use the two-phase lifecycle: the content library is reached while initializing, not on the first mapped cue.

## [1.0.0]

First release.

### Added

- Aura audio: cues, categories, voice limits, fades, music channel, playlists, ambience and scene music.
