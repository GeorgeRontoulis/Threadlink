# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0]

First release.

### Added

- Sentinel accounts, capability routing and per-account MessagePack saves with recovery.
- Platform-agnostic IDs (`AchievementId`, `StatisticId`, `LeaderboardId`, `ProductId`) that Sentinel passes to providers unchanged; each platform authors and resolves its own data, and `Achievements`, `Statistics`, `Leaderboards` and `Products` are generated from every installed platform's authoring.
