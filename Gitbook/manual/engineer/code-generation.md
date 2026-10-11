# Identifier Domains and Code Generation

A single pipeline produces every enumeration under `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

## Domain kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | xxHash32 of the scope-qualified key (`scope/name`, trimmed, ignoring case) | Retained as `[Obsolete]` tombstone with value preserved | All domains except Iris |
| **Ordinal** | The lowest free index, allocated in source order and kept by the manifest; removing an entry leaves a gap that a later entry may fill | Removed outright | `Iris.Events` |

Identity is the default kind, guaranteeing that removal cannot shift another entry's value. Iris is ordinal because dispatch indexes `EventRegistry` with the value directly; the registry spans the highest value, so the gaps removals leave are harmless. As Iris values are never serialised, outright removal producing compilation failure is the correct failure mode.

## Sources

Domain entries originate from up to three sources, merged in order:

1. **Native entries**: a framework-owned `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`), with an empty scope.
2. **Injectors**: files named `{DomainName}.{Injector}.txt` within the injector directory, its subfolders included. The injector name becomes the **scope**, folded into each entry's hash key. `Iris.Events.User.txt` is the injector named `User`. Injectors are read in ordinal file-path order.
3. **Domain definitions**: a `.txt` at the top level of the definitions directory declares an entirely new enumeration named after the file, emitted through the shared `CustomDomain.Shell.txt` into namespace `Threadlink.User` and assembly `Threadlink.User.Generated`.

In every source, blank lines and lines starting with `//` are ignored, and an entry repeated with the same scope (ignoring case) is read once.

Injectors may extend domain definitions on identical terms to native domains, permitting a third-party module to supply content for a project-defined domain.

Modules use this mechanism. A module requiring its own Iris events ships `Iris.Events.MyModule.txt`; placing it in the injector directory constitutes the entire installation procedure. A module's entries cannot displace existing ones: values and member names already recorded in the manifest never change, and new entries only receive values and names that no existing entry holds.

> A module's injector filename forms part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` alters the scope and therefore every identity-domain value that injector contributes.

## Manifests

Each domain maintains a `{DomainName}.manifest.json` in a `Manifests` folder beside its generated script, recording every entry's key, member name, scope, value, seed offset and tombstone state (and, for mapped assets, the asset's GUID). Manifests are version-controlled artefacts and establish identity stability:

- An assigned member name survives regeneration unchanged.
- Name collisions resolve deterministically: a second entry claiming `Splash` is qualified with its scope, as `GroupName_Splash`, then numbered (`Splash_2`, `Splash_3`, …) if that is taken too. The incumbent is never displaced.
- Member names are sanitised: a first character that is not a letter or underscore, and any later character that is not a letter, digit or underscore, become underscores.
- Removed entries are flagged as tombstoned rather than discarded. An entry declared again is revived from its tombstone.
- Hash collisions, and collisions with reserved values, are detected and rehashed under a recorded seed offset, rendering the resolution reproducible.

Each pass that changes a domain emits a summary of additions, removals, renames, rescopes and collisions through Scribe. A collision that cannot be resolved is reported as an error, and that domain is left untouched.

## Shells

A **shell** supplies the C# scaffolding for a generated file: namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` substitution token. `CustomDomain.Shell.txt` carries an additional `{DOMAIN_NAME}` token, permitting one template to serve every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator is configured to reserve those values. Shells do not declare generated entries. The Iris shell declares no sentinel, so Iris values start at zero.

## Generation triggers and guards

An `AssetPostprocessor` monitors every native-entries file and the injector, Addressables-injector and definition directories, regenerating when a `.txt` there is imported, deleted or moved. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a pass.

The pipeline enforces:

- Output confinement to the configured generated directories, preventing a misconfigured domain from overwriting hand-authored source.
- Abort on two domains resolving to the same output path.
- Diagnostic reporting, by filename, of any injector whose prefix matches no declared domain.
- Rejection of injectors carrying more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the sole accepted form.
- Emission of a sentinel-only enumeration (plus any surviving tombstones), with warning, for a domain yielding zero entries.
- A domain that fails is left untouched; the others are still generated.

## Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** enumerates every writable Addressable group with its member assets and a per-asset selection toggle. **Apply** performs:

1. Emission of one injector per group per reference kind (`Addressables.Assets.{Group}.txt` and equivalents) into the Addressables injector directory.
2. Regeneration of the three Addressables domains.
3. Reconstruction of the User Config reference maps from the resulting manifests, including each asset's main type and the aliases of renamed assets (see [Resource Loading](resource-loading.md)).

Group names are sanitised into scopes: `Test Assets` yields `Test_Assets`. Two groups sanitising to an identical scope are shown with a warning, and Apply refuses to run until one is renamed.

Apply writes changed files in an import batch, preserves existing file GUIDs, and removes obsolete injectors after their replacements exist. Identical files are not rewritten. Deselection still unmaps. Injector files in that directory are generated output and are not hand-edited.

**Reset Addressables manifests and apply** is the secondary button. After confirmation it clears the history (including tombstones) of the Scenes, Prefabs and Assets manifests and rebuilds them from the selected mappings. Other domains are untouched. Collision-resolved IDs can change and removed IDs may be reused, so use it only when the team intends a compatibility reset; normal Apply retains manifest history. Each file replacement is atomic, but the complete multi-file operation is not crash-atomic.

Editor tools map through the same logic with `ThreadlinkAddressablesMapping` (`Threadlink.Editor.CodeGen`). A tool that creates Addressable content maps it without a manual step:

```csharp
// Exactly what ticking the rows and selecting Apply does; existing mappings are kept.
if (ThreadlinkAddressablesMapping.Map("Assets/Content/Scenes/Farm/Farm.unity") is false)
    return; // Not an entry of an editable Addressable group; the reason is logged and nothing is applied.

bool mapped = ThreadlinkAddressablesMapping.IsMapped("Assets/Content/Scenes/Farm/Farm.unity");
List<string> unmapped = ThreadlinkAddressablesMapping.FindUnmapped(assetPaths);
```

`Map` accepts one or more paths, and applies nothing when every entry is mapped already. `FindUnmapped` returns the paths among those given that are not mapped. `ThreadlinkAddressablesMapping.Apply()` explicitly reapplies the current mappings without resetting manifest history. Applying regenerates the domains and recompiles scripts when needed, so code that needs new identifiers runs after the domain reload, for example from an `[InitializeOnLoad]` reconciler. An open Mapping Window refreshes itself after `Map`.

## Editor Config composition

Each entry in the Editor Config's `nativeDomains` array declares a domain name, an output filename (the domain name when empty), a shell, optional native entries, and three flags:

| Flag | Semantics |
|---|---|
| `ordinalValues` | Dense positional allocation. Applies to `Iris.Events` exclusively. |
| `reserveZero` | The shell declares a sentinel at zero that the allocator must not issue. On by default. |
| `sourcedFromAddressables` | Also draws injectors from the Addressables injector directory. |

`domainName` is the prefix injector filenames must match. No validation constrains it, so a mistyped name renders the corresponding injector unread. The orphaned-injector diagnostic addresses this case.

The Editor Config also assigns the generated scripts folder, the injector and Addressables-injector folders, and the domain-definition folder, scripts folder and shell. The shipped Editor Config declares every native domain listed in [Identifier Domains](../reference/identifier-domains.md).
