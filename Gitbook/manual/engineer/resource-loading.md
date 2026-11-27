# Resource Loading

Content loads by generated, typed IDs through the core (`com.threadforge.threadlink`), with Addressables as the default backend.

## Content mapping

Addressables is the source of truth for content: groups, addresses, labels, delivery and builds. The **Content mapping** (**Threadlink ▸ Content Mapping**, the Workspace's Content page) ticks the Addressables entries game code loads by ID. Applying writes three read-only dictionaries in **Project Settings ▸ Threadlink ▸ Content** (`ContentSettings`), one per load strategy, and generates their IDs:

| Load strategy | Dictionary | Generated class |
| --- | --- | --- |
| Any asset (`UnityEngine.Object`) | `ContentAssetId` → `AssetReference` | `ContentAssets.Group.Name` |
| Prefab (`GameObject` or a root component) | `ContentPrefabId` → `AssetReferenceGameObject` | `ContentPrefabs.Group.Name` |
| Scene (`SceneInstance`) | `ContentSceneId` → `AssetReference` | `ContentScenes.Group.Name` |

- An ID's value derives from the entry's asset GUID when it is first mapped and never changes, so renaming, moving or regrouping the asset keeps serialized fields and saves valid. The generated member follows the asset's current group and name; an unmapped entry stays as an obsolete tombstone.
- Fields of type `ContentAssetId`, `ContentPrefabId` or `ContentSceneId` show a dropdown of the mapped entries, so designer data refers to content without strings.
- Tools map entries through `ContentMapping.Map(paths)`, `Unmap`, `IsMapped` and `Apply`, exactly as ticking them on the page does.
- Assets load through their Addressables reference. The **Content** validator reports mapped entries that are no longer addressable, whose kind changed, or whose group excludes GUIDs from the catalog (**Include GUIDs in Catalog**).

## Loading

```csharp
var portrait = await Threadlink.LoadAsync<Sprite>(ContentAssets.Characters.HeroPortrait, owner: this);  // released with this
icon.sprite = portrait.Asset;

var slime = await Threadlink.InstantiateAsync<Enemy>(ContentPrefabs.Characters.Slime, spawn.position, spawn.rotation);
slime.Asset.Alert();                       // the prefab's root component, typed
slime.Release();                           // destroys the instance

var cave = await Threadlink.LoadSceneAsync(ContentScenes.Levels.Cave);              // or Nexus.LoadAsync for a transition
var forest = await Threadlink.LoadAllAsync<AudioCue>(ContentLabels.Forest, owner: this); // every asset with the label
```

| API | Behaviour |
| --- | --- |
| `Threadlink.LoadAsync<T>(ContentAssetId[, token])` / `(id, owner)` | Loads an asset as `T`, any `UnityEngine.Object` type it can load as. One request per call; the asset stays loaded until the request is released, or until the `MonoBehaviour` owner is destroyed. |
| `Threadlink.LoadAllAsync<T>(ids)` | Loads several assets; if any fails, the others are released. |
| `Threadlink.LoadAsync(ContentPrefabId)` / `LoadAsync<T>(ContentPrefabId)` | Loads a prefab without instantiating it, as a `GameObject` or its root component `T`. |
| `Threadlink.InstantiateAsync(ContentPrefabId[, position, rotation, parent])`, `InstantiateAsync<T>(…)` / `(id, owner)` | Instantiates a prefab, as a `GameObject` or its root component `T`; releasing the request destroys the instance. |
| `Threadlink.LoadSceneAsync(ContentSceneId)` | An additive scene you manage yourself. Use [Nexus](nexus.md) for transitions. |
| `Threadlink.LoadAllAsync<T>(label[, token])` / `(label, owner)` | Every asset with an Addressables label as one request (`ResourceRequest<IList<T>>`); releasing it releases them all. A component type loads labelled prefabs and holds each root's component. |
| `ResourceRequest<T>` | `Asset`, `IsReleased`, `Release()` (idempotent), `ReleaseAsync()` (awaits backend cleanup), `Dispose()`. |
| `Threadlink.MapContent(id, key)` | Maps an ID to a backend key from code, for tests, tools and content generated at runtime. |
| `Threadlink.UseContentBackend(backend)` | Loads through another `IContentBackend` (Resources, streamed or generated content); `null` returns to Addressables. |

Requests never share a hidden cache: two loads are two obligations, and Addressables keeps the reference counting. An unmapped ID throws `KeyNotFoundException`; loading an asset as a component, or a prefab as an asset type, throws before anything loads; a prefab without the requested root component is released and throws `InvalidOperationException`. A custom `IContentBackend` implements `Acquire` and, for labels, `AcquireLabel`. Outstanding requests appear in the Runtime monitor under Content, and any still held when the session ends are logged.

