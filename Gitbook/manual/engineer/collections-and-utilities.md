# Collections and Utilities

## Serialisable maps

Serialisable maps reside in `Threadlink.Collections`. Both derive from `ThreadlinkHashMap<TKey, TValue>`: bucket-indexed, allocation-free on lookup, and rebuilt by `ISerializationCallbackReceiver` after deserialisation.

| Type | Value backing | Application |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` | Value types and Unity object references |
| `RefHashMap<K,V>` | `[SerializeReference]` | Polymorphic managed values, including Vault's `DataField` |

Runtime access is read-only: `Count`, `ContainsKey`, `TryGetValue`, the indexer's getter (a missing key logs a `KeyNotFoundException` and returns `default` rather than throwing), `Keys` and `Values` as spans, `KeyMemory` and `ValueMemory` as memory, `For(action)` to visit every entry by reference, and `Clear()`.

Editor-only mutation is exposed through `EditorOnly_TryAdd`, `EditorOnly_Add`, `EditorOnly_Remove`, and the indexer's setter. `OnAfterDeserialize` clamps a serialised entry count exceeding the backing arrays and reports the discrepancy rather than throwing from a deserialisation callback.

## Serialisable arrays and lists

`Threadlink.Collections` also provides indexable wrappers for serialised collections, each exposing `Collection` as an `IList<T>` and an indexer:

| Type | Backing |
|---|---|
| `FieldArray<T>`, `FieldList<T>` | `[SerializeField]` array or list |
| `RefArray<T>`, `RefList<T>` | `[SerializeReference]` array or list, with a type picker for polymorphic elements |

Arrays expose `Length`; lists expose `Count`.

## Extension libraries

Under `Threadlink.Utilities`:

```csharp
using Threadlink.Utilities.Mathematics;   // float.IsSimilarTo(b), MoveTowards(target, maxDelta), NormalizeBetween,
                                          // ToUVCoordinates, ToScreenPosition, CubicInterpolation
using Threadlink.Utilities.Vectors;       // Vector3.IsSimilarTo(b), Clamp, ClampWithFallback, VectorTo; Vectors.Half, Vectors.XZ
using Threadlink.Utilities.Strings;       // string.ToAbsolutePath(), ToProjectRelativePath(), WithUpperCaseSpacing(),
                                          // ToNonAllocText(), ReadLines, DeserializeIntoDictionaryAsync
using Threadlink.Utilities.UniTask;       // List<UniTask>.AwaitAllThenClear(trim)
using Threadlink.Utilities.Collections;   // IDisposable.PreventEditorMemoryLeaks(), IsWithinBoundsOf, DisposeSafely,
                                          // EnsureSize, SerializeIntoBinary (Editor)
using Threadlink.Utilities.Flags;         // HasFlagUnsafe, AddFlag, RemoveFlag, SetFlag, ClearFlag; EnumUtilities
using Threadlink.Utilities.Objects;       // As<T>, Set<T>, Clone<T>, IsPrefabAssetInProject
using Threadlink.Utilities.Localization;  // GetSafeLocalizedString, TryGetLocalizedString, GetLocalizedStringAsync, GetSafeComment
using Threadlink.Utilities.RNG;           // System.Random.NextNonZeroUInt(), NextUInt64()
using Threadlink.Utilities.Attributes;    // [MinMaxRange], [ReadOnly], [HashMapDrawer]
```

`Threadlink.Utilities.RNG` extends `System.Random` for non-deterministic needs only; anything reproducible draws from [StatelessRNG](deterministic/stateless-rng.md).

## Attributes

- **`[MinMaxRange(min, max)]`** draws a range slider for a `Vector2`, `Vector2Int`, `float2` or `int2` field. The float form takes an optional number of displayed decimals (0–3, default 1).
- **`[ReadOnly]`** is a marker attribute without an associated drawer; supplying one would displace the hash-map drawer, as attribute drawers take precedence over type drawers. `ThreadlinkHashMapDrawer` reads the attribute from `fieldInfo` and renders the map without addition, removal or reordering controls. The Odin drawer honours both this attribute and Odin's own `ReadOnly`.
- **`[HashMapDrawer(mode)]`** selects which inspector draws a hash map: `Automatic` (Odin's when Odin is installed, otherwise Threadlink's), `Native` (always Threadlink's), or `Odin` (Odin's, falling back to Threadlink's when Odin is absent).

## Shared types and helpers

In `Threadlink.Shared`:

| Type | Purpose |
|---|---|
| `HashFunctions` | `ToXxHash32` and `ToXxHash64` of a string (trimmed and lower-cased first, optionally seeded), and `ToSplitMix64`. The framework's name hashing, used for object, subsystem and identifier IDs. |
| `AssetReferenceExtensions` | `LoadSynchronously<T>`, `LoadAsync<T>`, and scene `LoadAsync` / `UnloadAsync` for Addressables references (see [Resource Loading](resource-loading.md)). |
| `AddressablesRequest<T>` | A fixed-capacity, disposable batch of resource identifiers. |
| `byte2`, `ushort2` | Compact two-component integer structs; `ushort2` converts to and from Mathematics' `int2` and `uint2`. |
| `ThreadlinkConfigFinder` (Editor) | `TryGetConfig<T>(out T)` finds and caches the project's single asset of a configuration type. |

## Animation helpers

| Type | Purpose |
|---|---|
| `AnimatorHash` (`Create ▸ Threadlink ▸ Animation ▸ Animator Hash`) | An asset holding a precomputed `Animator.StringToHash` value, exposed as `Value`. Enter the parameter or state name and generate the hash from the asset's context menu (or button, under Odin). |
| `AnimationEventsReceiver` | A `LinkableBehaviour` beside an `Animator` whose `Receive…` methods are animation-event targets. It re-raises them as C# events with the animator: `OnVoidReceived`, `OnFloatReceived`, `OnIntegerReceived`, `OnStringReceived`, `OnUnityObjectReceived`, `OnVaultReceived` (when the object passed is a Vault) and `OnEventReceived`. |

Both reside in namespace `Threadlink.Animation`.
