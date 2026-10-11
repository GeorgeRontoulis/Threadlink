# Resource Loading

There are two matching APIs: instance members on the core that take a generated identifier, and static members that take an Addressables reference.

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

Asset and prefab loads return `null` if the identifier is unmapped or its reference is invalid; a prefab load also returns `null` if the prefab has no `T`. Synchronous loads block until Addressables finishes. A failed load is logged and its handle released. Loading an asset that is already loaded, or loading, reuses that operation.

Normally, load scenes through [Nexus](nexus.md). The scene members here are the layer beneath it, for code that manages a scene itself and so must also boot and discard its objects (see [Deployment and Initialisation](deployment.md#initialisation-pipeline)).

## Queries and validation

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes id, out Scene scene);
core.CheckIDValidity(id);                                                  // scenes, assets or prefabs
int found = core.CollectAssetIDs<T>(List<ThreadlinkIDs.Addressables.Assets> result);   // mapped assets of type T, none loaded
```

Every lookup goes through the User Config's maps and checks `RuntimeKeyIsValid()`, reporting failures through Scribe.

`CollectAssetIDs<T>` adds every mapped asset whose main type is `T` or derives from it, without loading any, and returns how many it added. The Mapping Window records each asset's main type in the User Config (`assetTypes`, as `"Namespace.Type, Assembly"`) when it applies. So code that needs every asset of a kind (a game's simulation content, say) asks for their IDs and loads them, without keeping its own catalogue. Assets mapped before types were recorded are found once the mappings are applied again.

## Renamed assets

An asset ID is a hash of its group and name, so renaming an asset or moving it to another group gives it a new ID and leaves the old one as a tombstone. The manifest records each mapped asset's GUID, so the next Apply finds the asset under its new name and records the old ID as an alias of the new one in the User Config (`assetAliases`).

```csharp
core.TryResolveRenamedAsset(oldID, out ThreadlinkIDs.Addressables.Assets currentID);
core.CollectRenamedAssets(List<(ThreadlinkIDs.Addressables.Assets OldID, ThreadlinkIDs.Addressables.Assets CurrentID)> result);
```

Data that stored the old ID, such as a save, can resolve or rewrite it. Aliases only exist for renames made after GUIDs were recorded; older entries get their GUIDs the next time the mappings are applied.

## Scene references

Scene loads are "load or get": loading a scene that is already loaded, or loading, returns that instance, whatever parameters are passed. `UnloadSceneAsync` completes once the scene has actually unloaded. A failed scene load or unload is reported through Scribe and returns a default `SceneInstance` rather than throwing.

`SceneAssetReference` tracks its load through `SceneOperation`, not `AssetReference.OperationHandle`, which Addressables only fills for `LoadSceneMode` loads. It adds `LoadSceneAsync(LoadSceneParameters)`, `UnloadSceneAsync(bool autoReleaseHandle)` and `TryGetLoadedScene(out Scene)`, and routes the base `LoadSceneAsync(LoadSceneMode)`, `UnLoadScene()` and `ReleaseAsset()` through the same operation.

The reference overloads let a portable module hold its own references without depending on a project's generated `Assets` enumeration. The extension methods underneath (`LoadSynchronously<T>`, `LoadAsync<T>`, and the scene `LoadAsync` / `UnloadAsync`) are public in `Threadlink.Shared.AssetReferenceExtensions`.

## Native resource batches

To load several framework resources at once, `ThreadlinkNativeConfig.LoadNativeResourcesAsync` takes an `AddressablesRequest<NativeResources>` and fills a dictionary you supply. The request is a fixed-capacity `UnsafeList<T>` (`new AddressablesRequest<T>(count, allocator)`, `Add`, `Dispose`); the loader runs the Addressables operations together using pooled task storage, then collects the results. Load a single native resource with `LoadNativeResourceAsync<T>(id)`.
