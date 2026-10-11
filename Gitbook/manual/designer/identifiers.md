# The Identifier Workflow

Threadlink compiles plain-text declaration lists into C# enumerations. The declaration files reside in **`Threadlink User/Native Domain Injectors/`**. Three are designer-owned:

| File | Resulting dropdown | Purpose |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Control contexts (`Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Locations at which entities may be placed. |
| `Vault.Fields.User.txt` | Vault Fields | Data fields available to Vault assets. |

These files are termed **injectors**: each injects entries into an enumeration the framework declares. Two further injectors in the same directory are engineer-owned (`Iris.Events.User.txt`, `StatelessRNG.Domains.User.txt`).

## Declaration syntax

Each file opens with comment lines prefixed `///`. Declarations follow, one identifier per line:

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

Constraints:

- One identifier per line. Leading and trailing whitespace is ignored.
- Identifiers begin with a letter or underscore and comprise letters, digits and underscores. Any other character is substituted with an underscore during generation; declaring `Checkpoint_Alpha` directly is preferred to relying on substitution.
- Lines prefixed `//`, including `///`, are treated as comments.
- Blank lines are ignored.
- Identifier comparison is case-insensitive: `Alpha` and `alpha` denote the same entry, and only the first is kept.

## Regeneration

Saving the file is sufficient. The injector directory is monitored, generation runs automatically, and Unity recompiles. New identifiers appear in the corresponding dropdowns.

**`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a generation pass on demand.

## Removal semantics

Entries may be removed by deleting the corresponding line. Values are content-addressed, with the following consequences:

- Removing an entry does not alter the value of any other entry.
- The removed identifier is retained in the generated enumeration as an obsolete tombstone, preserving resolution for assets that still reference it. Code referencing it produces a compiler warning.
- Declaring a removed identifier again revives it with its original value.
- Reordering declarations has no effect; declaration order is not significant.

Iris events constitute the sole exception and fall under engineering ownership. Their values are positional, so removal produces compilation failures at every subscription site rather than a tombstone. This behaviour is intentional.

## Rename semantics

A rename is equivalent to a removal followed by an addition: the former identifier becomes a tombstone and the new identifier receives a distinct value. References to the former identifier require reassignment. Engineering must be notified of any rename affecting an identifier referenced in code.

Each generation pass that changes a domain emits a console summary enumerating additions, removals, renames, rescopes and collisions. Consulting this summary is the most direct means of detecting an unintended rename.
