# Identifier Domains and Code Generation

One pipeline generates every enumeration in `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

## Domain kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | xxHash32 of the scope-qualified key (`scope/name`, trimmed, ignoring case) | Kept as an `[Obsolete]` tombstone, value unchanged | Every domain except Iris |
| **Ordinal** | The lowest free index, assigned in source order and kept by the manifest. A removal leaves a gap that a later entry may fill | Removed entirely | `Iris.Events` |

Identity is the default, so a removal can never shift another entry's value. Iris is ordinal because dispatch uses the value as an index into `EventRegistry`; the registry spans the highest value, so gaps are harmless. Iris values are never serialised, so a removal that breaks compilation is the right outcome.

## Sources

A domain's entries come from up to three sources, merged in this order:

1. **Native entries**: a framework `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`), with an empty scope.
2. **Injectors**: files named `{DomainName}.{Injector}.txt` anywhere in the injector folder. The injector name becomes the **scope**, which is part of each entry's hash key; `Iris.Events.User.txt` is the injector named `User`. Injectors are read in file-path order.
3. **Domain definitions**: a `.txt` at the top of the definitions folder declares a new enumeration named after the file, generated through the shared `CustomDomain.Shell.txt` into the `Threadlink.User` namespace and the `Threadlink.User.Generated` assembly.

In every source, blank lines and lines starting `//` are ignored, and an entry repeated in the same scope (ignoring case) counts once.

Injectors can extend project-defined domains exactly as they extend native ones, so a third-party module can add entries to a project's domain.

This is how modules install. A module that needs its own Iris events ships `Iris.Events.MyModule.txt`; dropping it into the injector folder is the whole installation. A module's entries can't displace existing ones: values and member names already in the manifest never change, and new entries only get values and names nobody holds.

> A module's injector filename is part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` changes the scope, and so every identity-domain value that injector contributes.

## Manifests

Each domain has a `{DomainName}.manifest.json` in a `Manifests` folder beside its generated script. It records every entry's key, member name, scope, value, seed offset and tombstone state (and, for mapped assets, the asset's GUID). Commit manifests to version control; they are what keeps identifiers stable:

- A member name, once assigned, never changes.
- Name collisions resolve the same way every time: a second `Splash` is qualified with its scope, as `GroupName_Splash`, then numbered (`Splash_2`, `Splash_3`, …) if that is taken too. The existing entry keeps its name.
- Member names are sanitised: a first character that isn't a letter or underscore, and any later character that isn't a letter, digit or underscore, becomes an underscore.
- Removed entries are marked as tombstones, not deleted. Declaring one again revives it.
- Hash collisions, including with reserved values, are detected and rehashed with a recorded seed offset, so the result is reproducible.

Each pass that changes a domain logs a summary of additions, removals, renames, rescopes and collisions through Scribe. A collision that can't be resolved is reported as an error, and that domain is left as it was.

## Shells

A **shell** is the C# template for a generated file: namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` token where entries go. `CustomDomain.Shell.txt` also has a `{DOMAIN_NAME}` token, so one template serves every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator reserves those values. Shells don't declare generated entries. The Iris shell has no sentinel, so Iris values start at zero.

## Generation triggers and guards

An `AssetPostprocessor` watches the native-entries files and the injector, Addressables-injector and definition folders, and regenerates when a `.txt` there is imported, deleted or moved. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** runs a pass on demand.

The pipeline:

- Only writes inside the configured generated folders, so a misconfigured domain can't overwrite hand-written code.
- Stops if two domains would write to the same file.
- Reports, by filename, any injector whose prefix matches no domain.
- Rejects injectors with more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the only accepted form.
- Generates just the sentinel (plus any tombstones), with a warning, for a domain with no entries.
- Leaves a failed domain as it was, and still generates the others.

## Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** lists every writable Addressable group, its assets, and a tick box per asset. **Apply**:

1. Writes one injector per group and reference kind (`Addressables.Assets.{Group}.txt` and so on) into the Addressables injector folder.
2. Regenerates the three Addressables domains.
3. Rebuilds the User Config's reference maps from the manifests, including each asset's main type and the aliases of renamed assets (see [Resource Loading](resource-loading.md)).

Group names become scopes: `Test Assets` becomes `Test_Assets`. Two groups that reduce to the same scope are flagged, and Apply won't run until one is renamed.

Apply writes changed files in one import batch, keeps existing file GUIDs, and removes old injectors only once their replacements exist. Unchanged files are not rewritten. Clearing a tick unmaps the asset. The injectors in that folder are generated; don't edit them.

The secondary button, **Reset Addressables manifests and apply**, asks for confirmation, then clears the history (tombstones included) of the Scenes, Prefabs and Assets manifests and rebuilds them from the current ticks. Other domains are untouched. IDs resolved after a collision can change and removed IDs can be reused, so use it only for a deliberate compatibility reset; a normal Apply keeps history. Each file is replaced atomically, but the operation as a whole is not crash-safe.

Editor tools can map through the same logic with `ThreadlinkAddressablesMapping` (`Threadlink.Editor.CodeGen`), so a tool that creates Addressable content needs no manual step:

```csharp
// Exactly what ticking the rows and selecting Apply does; existing mappings are kept.
if (ThreadlinkAddressablesMapping.Map("Assets/Content/Scenes/Farm/Farm.unity") is false)
    return; // Not an entry of an editable Addressable group; the reason is logged and nothing is applied.

bool mapped = ThreadlinkAddressablesMapping.IsMapped("Assets/Content/Scenes/Farm/Farm.unity");
List<string> unmapped = ThreadlinkAddressablesMapping.FindUnmapped(assetPaths);
```

`Map` takes one or more paths, and does nothing if they are all mapped already. `FindUnmapped` returns those of the given paths that aren't mapped. `ThreadlinkAddressablesMapping.Apply()` reapplies the current mappings without resetting history. Applying regenerates domains and recompiles when needed, so code that needs the new identifiers must run after the domain reload, for example from an `[InitializeOnLoad]` reconciler. An open Mapping Window refreshes itself after `Map`.

## Editor Config composition

Each entry in the Editor Config's `nativeDomains` array has a domain name, an output filename (the domain name if empty), a shell, optional native entries, and three flags:

| Flag | Meaning |
|---|---|
| `ordinalValues` | Positional values. Only for `Iris.Events`. |
| `reserveZero` | The shell declares a sentinel at zero, so the allocator never uses it. On by default. |
| `sourcedFromAddressables` | Also reads injectors from the Addressables injector folder. |

`domainName` is the prefix injector filenames must match. Nothing validates it, so a typo means its injectors are never read; the orphaned-injector report catches this.

The Editor Config also sets the generated scripts folder, the injector and Addressables-injector folders, and the domain-definition folder, scripts folder and shell. The shipped Editor Config declares every native domain in [Identifier Domains](../reference/identifier-domains.md).
