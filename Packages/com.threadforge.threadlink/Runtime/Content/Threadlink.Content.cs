namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using Object = UnityEngine.Object;

    public sealed partial class Threadlink
    {
        // ---- Assets: any UnityEngine.Object ---------------------------------------------------------------------------

        /// <summary>Loads a mapped asset as <typeparamref name="T"/>. Release the request when you no longer need it.</summary>
        public static async UniTask<ResourceRequest<T>> LoadAsync<T>(ContentAssetId asset, CancellationToken token = default)
            where T : Object => await Provider.LoadAsync(new ResourceId<T>(Require(asset.IsAssigned, asset.Identity, asset)), token);

        /// <summary>Loads a mapped asset held by <paramref name="owner"/>: released when the owner is destroyed.</summary>
        public static async UniTask<ResourceRequest<T>> LoadAsync<T>(ContentAssetId asset, MonoBehaviour owner)
            where T : Object => Own(owner, await LoadAsync<T>(asset, Token(owner)));

        /// <summary>Loads several mapped assets of one type; if any fails, the others are released.</summary>
        public static async UniTask<ResourceRequest<T>[]> LoadAllAsync<T>(IEnumerable<ContentAssetId> assets, CancellationToken token = default)
            where T : Object => await Provider.LoadBatchAsync((assets ?? throw new ArgumentNullException(nameof(assets))).Select(asset => new ResourceId<T>(Require(asset.IsAssigned, asset.Identity, asset))), token);

        // ---- Prefabs: GameObject or a root component --------------------------------------------------------------------

        /// <summary>Loads a mapped prefab without instantiating it.</summary>
        public static async UniTask<ResourceRequest<GameObject>> LoadAsync(ContentPrefabId prefab, CancellationToken token = default) =>
            await Provider.LoadAsync(Resource<GameObject>(prefab), token);

        /// <summary>Loads a mapped prefab and holds its root component <typeparamref name="T"/>, without instantiating it.</summary>
        public static async UniTask<ResourceRequest<T>> LoadAsync<T>(ContentPrefabId prefab, CancellationToken token = default)
            where T : Component => await Provider.LoadAsync(Resource<T>(prefab), token);

        /// <summary>Loads a mapped prefab held by <paramref name="owner"/>: released when the owner is destroyed.</summary>
        public static async UniTask<ResourceRequest<GameObject>> LoadAsync(ContentPrefabId prefab, MonoBehaviour owner) =>
            Own(owner, await LoadAsync(prefab, Token(owner)));

        /// <summary>Loads a mapped prefab's root component <typeparamref name="T"/>, held by <paramref name="owner"/>.</summary>
        public static async UniTask<ResourceRequest<T>> LoadAsync<T>(ContentPrefabId prefab, MonoBehaviour owner)
            where T : Component => Own(owner, await LoadAsync<T>(prefab, Token(owner)));

        /// <summary>Instantiates a mapped prefab. Releasing the request destroys the instance.</summary>
        public static async UniTask<ResourceRequest<GameObject>> InstantiateAsync(ContentPrefabId prefab, CancellationToken token = default) =>
            await Provider.AcquireAsync(Resource<GameObject>(prefab), AcquisitionKind.Instance, token);

        /// <summary>Instantiates a mapped prefab at a position. Releasing the request destroys the instance.</summary>
        public static async UniTask<ResourceRequest<GameObject>> InstantiateAsync(
            ContentPrefabId prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            CancellationToken token = default
        ) => Place(await InstantiateAsync(prefab, token), position, rotation, parent);

        /// <summary>Instantiates a mapped prefab held by <paramref name="owner"/>: destroyed when the owner is destroyed.</summary>
        public static async UniTask<ResourceRequest<GameObject>> InstantiateAsync(ContentPrefabId prefab, MonoBehaviour owner) =>
            Own(owner, await InstantiateAsync(prefab, Token(owner)));

        /// <summary>Instantiates a mapped prefab and holds its root component <typeparamref name="T"/>. Releasing the request destroys the instance.</summary>
        public static async UniTask<ResourceRequest<T>> InstantiateAsync<T>(ContentPrefabId prefab, CancellationToken token = default)
            where T : Component => await Provider.AcquireAsync(Resource<T>(prefab), AcquisitionKind.Instance, token);

        /// <summary>Instantiates a mapped prefab at a position and holds its root component <typeparamref name="T"/>.</summary>
        public static async UniTask<ResourceRequest<T>> InstantiateAsync<T>(
            ContentPrefabId prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            CancellationToken token = default
        )
            where T : Component => Place(await InstantiateAsync<T>(prefab, token), position, rotation, parent);

        /// <summary>Instantiates a mapped prefab held by <paramref name="owner"/> and holds its root component <typeparamref name="T"/>.</summary>
        public static async UniTask<ResourceRequest<T>> InstantiateAsync<T>(ContentPrefabId prefab, MonoBehaviour owner)
            where T : Component => Own(owner, await InstantiateAsync<T>(prefab, Token(owner)));

        // ---- Scenes: SceneInstance --------------------------------------------------------------------------------------

        /// <summary>Loads a mapped scene additively, for scenes you manage yourself. Scene transitions belong to Nexus.</summary>
        public static async UniTask<ResourceRequest<SceneInstance>> LoadSceneAsync(ContentSceneId scene, CancellationToken token = default) =>
            await Provider.AcquireAsync(new ResourceId<SceneInstance>(Require(scene.IsAssigned, scene.Identity, scene)), AcquisitionKind.Scene, token);

        // ---- Labels -----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Loads every asset with an Addressables label, as one request; releasing it releases them all. A component type
        /// loads the labelled prefabs and holds each root's component.
        /// </summary>
        public static async UniTask<ResourceRequest<IList<T>>> LoadAllAsync<T>(ContentLabel label, CancellationToken token = default) =>
            await Provider.LoadLabelAsync<T>(label, token);

        /// <summary>Loads every asset with an Addressables label, held by <paramref name="owner"/>: released when the owner is destroyed.</summary>
        public static async UniTask<ResourceRequest<IList<T>>> LoadAllAsync<T>(ContentLabel label, MonoBehaviour owner) =>
            Own(owner, await Provider.LoadLabelAsync<T>(label, Token(owner)));

        // ---- Mapping from code ------------------------------------------------------------------------------------------

        /// <summary>Maps an asset ID to an Addressables (or custom backend) key from code, for tests, tools and generated content.</summary>
        public static void MapContent(ContentAssetId asset, string key) =>
            ContentLibrary.AddEntries(new[] { ContentEntry.Create(Require(asset.IsAssigned, asset.Identity, asset), typeof(Object), key) });

        /// <summary>Maps a prefab ID to an Addressables (or custom backend) key from code.</summary>
        public static void MapContent(ContentPrefabId prefab, string key) =>
            ContentLibrary.AddEntries(new[] { ContentEntry.Create(Require(prefab.IsAssigned, prefab.Identity, prefab), typeof(GameObject), key) });

        /// <summary>Maps a scene ID to an Addressables (or custom backend) key from code.</summary>
        public static void MapContent(ContentSceneId scene, string key) =>
            ContentLibrary.AddEntries(new[] { ContentEntry.Create(Require(scene.IsAssigned, scene.Identity, scene), typeof(SceneInstance), key) });

        /// <summary>
        /// Loads content through <paramref name="backend"/> instead of Addressables, for Resources, streamed or generated
        /// content. Takes effect immediately; requests made through the previous backend release through it. Pass null to return to Addressables.
        /// </summary>
        public static void UseContentBackend(IContentBackend backend) => ContentLibrary.Instance.UseBackend(backend);

        private static ContentProvider Provider => ContentLibrary.Instance.Provider;

        private static ResourceId<T> Resource<T>(ContentPrefabId prefab) => new ResourceId<T>(Require(prefab.IsAssigned, prefab.Identity, prefab));

        private static Guid Require(bool assigned, Guid identity, object id) =>
            assigned ? identity : throw new ArgumentException("The content ID is empty: " + id + ".", nameof(id));

        private static ResourceRequest<T> Place<T>(ResourceRequest<T> request, Vector3 position, Quaternion rotation, Transform parent)
            where T : Object
        {
            var transform = request.Asset is Component component ? component.transform : ((GameObject)(Object)request.Asset).transform;
            if (parent != null)
                transform.SetParent(parent, false);
            transform.SetPositionAndRotation(position, rotation);
            return request;
        }

        private static CancellationToken Token(MonoBehaviour owner) =>
            owner != null ? owner.destroyCancellationToken : throw new ArgumentNullException(nameof(owner));

        private static ResourceRequest<T> Own<T>(MonoBehaviour owner, ResourceRequest<T> request)
        {
            if (owner == null)
            {
                request.Release();
                throw new OperationCanceledException("The owner was destroyed while loading.");
            }
            owner.destroyCancellationToken.Register(static state => ((ResourceRequest<T>)state).Release(), request);
            return request;
        }
    }
}
