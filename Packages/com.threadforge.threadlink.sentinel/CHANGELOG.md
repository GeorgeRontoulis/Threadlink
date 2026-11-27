# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0]

No changes; released with the framework's shared version.

## [3.0.0]

### Changed

- `CapabilityStatus.Supported` is `IsSupported`; `AutosaveSchedule.Paused` is `IsPaused`.
- `ICommerce.CatalogAsync` and `RestoreAsync` are `ReadCatalogAsync` and `RestorePurchasesAsync`.
- `SnapshotSchema.RegisterMigration` and `RegisterLegacyReader` are `AddMigration` and `SetLegacyReader`, matching `Sentinel.Saves.AddMigration`.
- Save storage contracts name identifiers `saveId`, `fileId` and `SaveId`.

### Removed

- `SentinelComposition`, a second copy of Sentinel's routing that nothing used: route providers with `Sentinel.AddProvider`.

## [2.0.0]

No changes; released with the framework's shared version.

## [1.1.0]

### Added

- Platform events on Iris: `PlatformReady`, `AccountChanged` (including the startup account), `JoinIntentReceived` and `AchievementUnlocked`.
- Save events on Iris: `SaveWritten`, `SaveFailed` and `SaveRecovered`.

### Changed

- Sentinel boots on its own and announces the startup account while initializing.

## [1.0.0]

First release.

### Added

- Sentinel accounts, capability routing and per-account MessagePack saves with recovery.
- Platform-agnostic IDs (`AchievementId`, `StatisticId`, `LeaderboardId`, `ProductId`) that Sentinel passes to providers unchanged; each platform authors and resolves its own data, and `Achievements`, `Statistics`, `Leaderboards` and `Products` are generated from every installed platform's authoring.
