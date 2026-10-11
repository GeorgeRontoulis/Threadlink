# The Identifier Workflow

Threadlink turns plain-text lists into C# enumerations. The lists are in **`Threadlink User/Native Domain Injectors/`**. Designers own three of them:

| File | Resulting dropdown | Purpose |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Control contexts (`Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Places where entities can be put. |
| `Vault.Fields.User.txt` | Vault Fields | The fields Vaults can hold. |

These files are called **injectors**, because each adds entries to an enumeration the framework declares. Engineers own the other two in the folder (`Iris.Events.User.txt`, `StatelessRNG.Domains.User.txt`).

## Declaration syntax

Each file starts with comment lines beginning `///`, followed by one identifier per line:

```text
///Use this file to define custom player spawn points for Nexus as showcased below:
///
///UserDefinedSpawnPoint1
///UserDefinedSpawnPoint2
///...
PlayerStart
BossArenaEntrance
SecretRoom_North
CheckpointAlpha
```

Rules:

- One identifier per line. Surrounding whitespace is ignored.
- Start with a letter or underscore; use only letters, digits and underscores. Other characters become underscores, but write `Checkpoint_Alpha` yourself rather than relying on that.
- Lines starting `//` (including `///`) are comments. Blank lines are ignored.
- Case doesn't matter: `Alpha` and `alpha` are the same entry, and only the first is kept.

## Regeneration

Save the file. Generation runs automatically, Unity recompiles, and the new identifiers appear in their dropdowns.

**`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** runs generation on demand.

## Removal semantics

To remove an entry, delete its line. Because each value is a hash of its name:

- Removing an entry never changes another entry's value.
- The removed identifier stays in the enumeration as an obsolete tombstone, so assets that still use it keep working. Code that uses it gets a compiler warning.
- Declaring it again brings it back with its original value.
- Order doesn't matter; reordering changes nothing.

The one exception is Iris events, which engineers own. Their values are positional, so removing one breaks compilation wherever it is used, instead of leaving a tombstone. This is deliberate.

## Rename semantics

A rename is a removal plus an addition: the old name becomes a tombstone and the new name gets a new value. Anything that used the old name must be reassigned, so tell engineering about any rename of an identifier used in code.

Each generation pass that changes a domain logs a summary of additions, removals, renames, rescopes and collisions. Check it to catch an accidental rename.
