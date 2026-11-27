# Serialization

Sentinel saves (`[Save]` types) are MessagePack payloads in a versioned envelope. This page describes the format and the lower-level `SnapshotSchema<T>` and `ISnapshotCodec<T>` used underneath and by custom storage.

## Snapshot envelope

`SnapshotSchema<T>.Write` produces a 28-byte header followed by the codec's payload:

| Offset | Bytes | Content |
|---|---|---|
| 0 | 4 | ASCII `TLSP` |
| 4 | 16 | Schema identity GUID |
| 20 | 4 | Schema version, little-endian |
| 24 | 4 | Payload length, little-endian |
| 28 | n | Codec payload |

`Read` checks the identity, version and length, decodes with the codec (or a registered migration reader), and runs the schema's validation on the result. Validation also runs before every write. `MaximumBytes` (16 MiB by default) bounds both directions.

The envelope layout is part of the save format. Sentinel's tests keep recorded envelopes as a regression check, so a change to the layout or the codec's output fails them. For `[Save]` types the identity is derived from the save name.

## MessagePack codec

```csharp
[GeneratedMessagePackResolver]
public partial class SnapshotResolver { }

[MessagePackObject]
public sealed partial class Progress
{
    [Key(0)] public int Score { get; set; }
    [Key(1)] public string[] Inventory { get; set; } = Array.Empty<string>();
}

var schema = new SnapshotSchema<Progress>(
    new Guid("…"), 1,
    new MessagePackSnapshotCodec<Progress>(CompositeResolver.Create(SnapshotResolver.Instance, BuiltinResolver.Instance)),
    value => { if (value.Score < 0) throw new ArgumentException("Invalid snapshot."); });
```

`[Save]` types need only `[MessagePackObject]` and `[Key]`: the bundled source generator creates their formatters, which Sentinel's standard resolver finds, so IL2CPP needs no runtime code generation. For custom schemas use the generator with an explicit resolver as above. Keep `[Key]` indices stable: add new members with new keys and never reuse a removed key. Assembly definitions that override references list `MessagePack.dll` and `MessagePack.Annotations.dll` as precompiled references.

Map Unity value types (vectors, colors and similar) to plain fields in the snapshot type; this keeps saves independent of engine types. Projects that prefer MessagePack's own Unity formatters can add the upstream MessagePack-CSharp Unity package from its Git URL.

## Versions and migrations

Raise the schema version when the payload layout changes, and register a reader for each older version with `RegisterMigration`. A reader receives the old payload and returns a current, valid snapshot. Bytes without the `TLSP` header are read only through an explicit `RegisterLegacyReader`; an unknown `TLSP` version never falls back to it. Reading through a migration does not rewrite storage. See [Transactional Saving](sentinel/saving.md#envelope-and-migration) for slots, storage and recovery.
