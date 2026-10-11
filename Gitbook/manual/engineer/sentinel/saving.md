# Transactional Saving

`ISaveService` operates exclusively on byte arrays. A logical save is identified by a `saveID` and holds any number of files, each identified by a `fileID`.

```csharp
var transactionResult = await saves.BeginTransactionAsync(saveID);

if (transactionResult.Succeeded)
{
    var transaction = transactionResult.Value;

    var write = await transaction.WriteAsync(fileID, bytes);

    if (write.Succeeded)
        await transaction.CommitAsync();

    transaction.Discard();
}
```

| Member | Semantics |
|---|---|
| `ISaveService.ReadAsync(saveID, fileID)` | Reads one file of the published save. A missing save or file fails with `NotFound`. |
| `ISaveService.BeginTransactionAsync(saveID)` | Starts a transaction based on the published save. |
| `ISaveService.DeleteSaveAsync(saveID)` | Deletes the save logically, with the same atomic publication guarantee. |
| `ISaveTransaction.ReadAsync`, `WriteAsync`, `DeleteAsync` | Read, write or delete one file within the transaction. |
| `ISaveTransaction.CommitAsync()` | Publishes the transaction's generation; `IsCommitted` reports it. |
| `ISaveTransaction.Discard()` | Releases the transaction, committed or not. |

A transaction prepares a complete logical save while leaving the currently published generation untouched. `CommitAsync()` atomically publishes the new generation; failed or abandoned transactions leave the previous published state intact. Save ID 0 and file ID 0 are reserved.

Serialisation remains the caller's responsibility. `Threadlink.TrySerialize<T>` and `Threadlink.TryDeserialize<T>` provide MessagePack wrappers (with optional `MessagePackSerializerOptions`, and a `ReadOnlyMemory<byte>` overload for deserialisation), but Sentinel itself stores opaque bytes.

## Storage by module

| Module | Storage |
|---|---|
| **Local** | Generation directories under `Application.persistentDataPath/Sentinel/<saveID>/`. A transaction is staged under `.staging`, flushed, and moved into the immutable `generations` directory; publication is the atomic replacement of the save's `current.ptr`. Older generations stay valid for readers that already observed them, and deletion is a pointer tombstone. |
| **Steam** | Steam Remote Storage, one file per published generation, keeping the newest `SteamSentinelSettings.RetainedSaveGenerations` (default 3). A generation is limited to Steam's 100 MB file-write limit and checked against the available quota. Steam Cloud synchronises it when enabled for the app and the account. |
