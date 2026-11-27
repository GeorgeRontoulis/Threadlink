# Transactional Saving

## Declaring save data

Declare each save type once: a stable name and current version with `[Save]`, serialized with MessagePack. Rename the class freely; never change the name.

```csharp
using MessagePack;
using Threadforge.Threadlink;

[Save("game", 2)]
[MessagePackObject]
public sealed partial class GameSave
{
    [Key(0)] public int Coins { get; set; }
    [Key(1)] public string Level { get; set; } = "Forest";
}
```

## Saving and loading

```csharp
var saved = await Sentinel.Saves.SaveAsync(0, game, "Forest, 12 coins");
if (!saved.Succeeded)
    ShowError(saved.Error);                       // Conflict, CorruptData, Unsupported, ...

var loaded = await Sentinel.Saves.LoadAsync<GameSave>(0);
if (loaded.Succeeded)
    Restore(loaded.Value);
else if (loaded.Error == PersistenceError.CorruptData)
    await Sentinel.Saves.RestorePreviousAsync<GameSave>(0);
```

| API | Behaviour |
| --- | --- |
| `Sentinel.Saves.SaveAsync(slot, data, title)` | Writes a slot atomically for the current account. Fails with `Conflict` if the slot changed since this session last read or wrote it. |
| `LoadAsync<T>(slot)` | `NotFound` for an empty slot, `CorruptData` for damaged data; older versions are migrated. |
| `List<T>()` | Slots that hold a save of the type. |
| `DeleteAsync<T>(slot)` | Deletes a slot; earlier versions stay recoverable. |
| `RestorePreviousAsync<T>(slot)` | Republishes the newest earlier version after validating it. |
| `AddMigration<T>(fromVersion, read)` | Reads payloads written by an older version. Call it from your save subsystem's `OnSubsystemInit`. |

Slots are numbered from 0 per save type. Saves live under `persistentDataPath/Threadlink/<Save Folder>` in a separate folder per account; switching account switches folders and never moves data. The folder name and size limit are engineering settings (**Project Settings ▸ Threadlink ▸ Sentinel**).

## Guarantees

A write prepares a new generation, flushes it, then atomically replaces the current pointer. A failure before publication leaves the previous save untouched; once admitted, a commit reports its real outcome even if the caller cancels. A filesystem without atomic replacement returns `Unsupported`. SHA-256 detects accidental corruption (it is not encryption or a signature). Reading through a migration does not rewrite storage; save again to publish the converted data. Use an application-private folder that no other process edits.

## Envelope and migration

Every save carries a 28-byte TLSP envelope: magic, schema identity (derived from the save name), version, payload length and the MessagePack payload. Unknown versions never fall back to another reader. Keep old readers for every version players may still have.

## Lower-level building blocks

`SnapshotSchema<T>`, `SnapshotStore<T>`, `LocalSaveService`, `AutosaveSchedule`, `MessagePackSnapshotCodec<T>` and `ListRecoveryVersions`/`RecoverAsync` remain available for tools and custom storage. `AutosaveSchedule.AdvanceAsync(elapsed, consistent, save)` runs at most one save at a time and only at a consistent moment. `CloudSynchronization<T>` keeps local and remote revisions separate and never picks a winner automatically; `PurchaseFulfillment<T>` saves a grant and its transaction ledger atomically before acknowledging the purchase.
