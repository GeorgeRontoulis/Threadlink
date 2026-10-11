# Resource Loading

Two parallel APIs exist: one keyed by generated identifier on the core instance, one accepting an Addressables reference directly as static members.

```csharp
core.LoadAsset<T>(ThreadlinkIDs.Addressables.Assets id);
Threadlink.LoadAsset<T>(AssetReference reference);
await core.LoadAssetAsync<T>(id);
await Threadlink.LoadAssetAsync<T>(reference);

core.LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs id);       // T : Component
Threadlink.LoadPrefab<T>(AssetReferenceGameObject reference);
await core.LoadPrefabAsync<T>(id);
await Threadlink.LoadPrefabAsync<T>(reference);

await core.LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes id, LoadSceneMode mode);
await core.LoadSceneAsync(id, new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D));
await core.UnloadSceneAsync(id);

core.ReleaseAsset(id);
core.ReleasePrefab(id);
```

Asset and prefab loads return `null` when the identifier is unmapped or its reference invalid; a prefab load also returns `null` when the prefab carries no `T`. Synchronous loads block until the Addressables operation completes. A failed load is logged and its handle released. Loading a reference whose asset is already loaded, or loading, reuses that operation.

Scenes are loaded through [Nexus](nexus.md) in normal use. The scene members here are the layer beneath it, for code that manages a scene by other means and therefore also boots and discards its objects (see [Deployment and Initialisation](deployment.md#initialisation-pipeline)).

## Queries and validation

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes id, out Scene scene);
core.CheckIDValidity(id);                                                  // scenes, assets or prefabs
int found = core.CollectAssetIDs<T>(List<ThreadlinkIDs.Addressables.Assets> result);   // mapped assets of type T, none loaded
```

All lookups resolve through the User Config's keyed maps and validate `RuntimeKeyIsValid()`, reporting failure through Scribe.

`CollectAssetIDs<T>` appends every mapped asset whose main type is `T` or derives from it, without loading any, and returns how many it appended. When the Mapping Window applies, it records each asset's main type in the User Config (`assetTypes`, as `"Namespace.Type, Assembly"`) beside its reference. A consumer that needs every asset of a kind (a game's simulation content, say) asks for their IDs and loads them, and keeps no catalogue of its own. Assets mapped before types were recorded are found once the mappings are applied again.

## Renamed assets

An asset ID is a hash of the group and the asset's name, so renaming an asset or moving it to another group gives it a new ID, and the old one becomes a tombstone. The manifest records each mapped asset's GUID, so applying the mappings finds the asset again under its new name and records the old ID as an alias of the new one in the User Config (`assetAliases`).

```csharp
core.TryResolveRenamedAsset(oldID, out ThreadlinkIDs.Addressables.Assets currentID);
core.CollectRenamedAssets(List<(ThreadlinkIDs.Addressables.Assets OldID, ThreadlinkIDs.Addressables.Assets CurrentID)> result);
```

Data that stored the old ID (a save) can resolve it or rewrite it. Aliases exist only for renames made after GUIDs were recorded: entries mapped earlier get theirs the next time the mappings are applied.

## Scene references

Scene loads are "load or get": loading a scene that is already loaded (or loading) returns that instance, whatever parameters are passed. `UnloadSceneAsync` completes once the scene has actually unloaded. A failed scene load or unload is reported through Scribe and returns a default `SceneInstance` instead of throwing.

`SceneAssetReference` tracks its load through `SceneOperation` rather than `AssetReference.OperationHandle`, which Addressables only populates for `LoadSceneMode` loads. `LoadSceneAsync(LoadSceneParameters)`, `UnloadSceneAsync(bool autoReleaseHandle)` and `TryGetLoadedScene(out Scene)` extend the base API. The base `LoadSceneAsync(LoadSceneMode)`, `UnLoadScene()` and `ReleaseAsset()` are routed through the same operation.

The reference overloads exist for portable modules: a module may hold its own references without depending on the consuming project's generated `Assets` enumeration. The underlying extension methods (`LoadSynchronously<T>`, `LoadAsync<T>`, and the scene `LoadAsync` / `UnloadAsync`) are public in `Threadlink.Shared.AssetReferenceExtensions`.

## Native resource batches

For framework-owned resource batches, `ThreadlinkNativeConfig.LoadNativeResourcesAsync` accepts an `AddressablesRequest<NativeResources>` and fills a caller-provided dictionary. The request uses a fixed-capacity `UnsafeList<T>` (`new AddressablesRequest<T>(count, allocator)`, `Add`, `Dispose`), and the loader batches Addressables operations through pooled task storage before collecting results. Single native resources load through `LoadNativeResourceAsync<T>(id)`.
