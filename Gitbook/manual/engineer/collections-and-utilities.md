# Collections and Utilities

## Serialisable maps

Serialisable maps are in `Threadlink.Collections`. Both derive from `ThreadlinkHashMap<TKey, TValue>`: bucket-indexed, allocation-free on lookup, and rebuilt by `ISerializationCallbackReceiver` after deserialisation.

| Type | Value backing | Use for |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` | Value types and Unity object references |
| `RefHashMap<K,V>` | `[SerializeReference]` | Managed values of varying types, such as Vault's `DataField` |

At runtime they are read-only: `Count`, `ContainsKey`, `TryGetValue`, the indexer's getter (a missing key logs a `KeyNotFoundException` and returns `default` instead of throwing), `Keys` and `Values` as spans, `KeyMemory` and `ValueMemory` as memory, `For(action)` to visit each entry by reference, and `Clear()`.

In the Editor, `EditorOnly_TryAdd`, `EditorOnly_Add`, `EditorOnly_Remove` and the indexer's setter modify them. If a serialised entry count exceeds the backing arrays, `OnAfterDeserialize` clamps it and reports the problem instead of throwing.

## Serialisable arrays and lists

`Threadlink.Collections` also wraps serialised collections. Each wrapper exposes `Collection` as an `IList<T>`, and an indexer:

| Type | Backing |
|---|---|
| `FieldArray<T>`, `FieldList<T>` | `[SerializeField]` array or list |
| `RefArray<T>`, `RefList<T>` | `[SerializeReference]` array or list, with a type picker for each element |

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

`Threadlink.Utilities.RNG` extends `System.Random` for non-deterministic uses only; anything reproducible uses [StatelessRNG](deterministic/stateless-rng.md).

## Attributes

- **`[MinMaxRange(min, max)]`** draws a range slider for a `Vector2`, `Vector2Int`, `float2` or `int2` field. The float form takes an optional number of displayed decimals (0–3, default 1).
- **`[ReadOnly]`** is a marker with no drawer of its own, since an attribute drawer would replace the hash-map drawer. `ThreadlinkHashMapDrawer` reads it from `fieldInfo` and draws the map without add, remove or reorder controls. The Odin drawer respects both this attribute and Odin's own `ReadOnly`.
- **`[HashMapDrawer(mode)]`** chooses which inspector draws a hash map: `Automatic` (Odin's if installed, otherwise Threadlink's), `Native` (always Threadlink's), or `Odin` (Odin's, or Threadlink's if Odin is missing).

## Shared types and helpers

In `Threadlink.Shared`:

| Type | Purpose |
|---|---|
| `HashFunctions` | `ToXxHash32` and `ToXxHash64` of a string (trimmed and lower-cased first, optionally seeded), and `ToSplitMix64`. The framework hashes names this way for object, subsystem and identifier IDs. |
| `AssetReferenceExtensions` | `LoadSynchronously<T>`, `LoadAsync<T>`, and scene `LoadAsync` / `UnloadAsync` for Addressables references (see [Resource Loading](resource-loading.md)). |
| `AddressablesRequest<T>` | A fixed-capacity, disposable batch of resource identifiers. |
| `byte2`, `ushort2` | Compact two-component integer structs; `ushort2` converts to and from Mathematics' `int2` and `uint2`. |
| `ThreadlinkConfigFinder` (Editor) | `TryGetConfig<T>(out T)` finds and caches the project's single asset of a configuration type. |

## Animation helpers

| Type | Purpose |
|---|---|
| `AnimatorHash` (`Create ▸ Threadlink ▸ Animation ▸ Animator Hash`) | An asset holding a precomputed `Animator.StringToHash` value as `Value`. Enter the parameter or state name, then generate the hash from the asset's context menu (or a button, with Odin). |
| `AnimationEventsReceiver` | A `LinkableBehaviour`, placed beside an `Animator`, whose `Receive…` methods are animation-event targets. It re-raises them as C# events with the animator: `OnVoidReceived`, `OnFloatReceived`, `OnIntegerReceived`, `OnStringReceived`, `OnUnityObjectReceived`, `OnVaultReceived` (when the object is a Vault) and `OnEventReceived`. |

Both are in the `Threadlink.Animation` namespace.
