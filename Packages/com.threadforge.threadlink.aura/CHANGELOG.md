# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0]

No changes; released with the framework's shared version.

## [3.0.0]

### Changed

- `AudioCue.Category` is the cue's `AudioCategory`; the serialized name is `CategoryName` (was the `Category` string field). `CategoryId` is removed. Existing cues keep their category.
- `AuraPlaylist.StartPlaylist` is `Play`. `PlaybackHandle` is created only by Aura.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Added

- `MusicChanged`, `VolumeChanged` and `MuteChanged` on Iris.

### Changed

- Aura and scene music use the two-phase lifecycle: the content library is reached while initializing, not on the first mapped cue.

## [1.0.0]

First release.

### Added

- Aura audio: cues, categories, voice limits, fades, music channel, playlists, ambience and scene music.
