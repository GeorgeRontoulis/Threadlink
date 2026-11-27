# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0]

### Changed

- `SteamProvider` implements the capability contracts explicitly: reach them through Sentinel (`Sentinel.Resolve<IStatistics>()`).
- A Steam leaderboard's `SteamName` is `ApiName`, as for achievements and statistics (`SteamOptions.Leaderboard(id, apiName)`). Existing settings keep their values. `SteamResults` is internal.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Changed

- Steam starts in the initialization phase, so it no longer requires Sentinel explicitly. Starting it announces `PlatformReady` and `AccountChanged` through Sentinel.
- The Steamworks.NET wrapper references its assembly by GUID.

## [1.0.0]

First release.

### Added

- Steam accounts, achievements, statistics, leaderboards, presence, friends, invites, DLC, join intents and Steam Cloud.
