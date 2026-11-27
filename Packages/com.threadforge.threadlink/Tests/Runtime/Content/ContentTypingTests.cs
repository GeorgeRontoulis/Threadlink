namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class ContentTypingTests
    {
        private sealed class Operation<T> : IResourceOperation<T>
        {
            internal int Releases;
            private readonly T value;

            internal Operation(T value) => this.value = value;

            public UniTask<T> Completion => UniTask.FromResult(value);
            public object Handle => this;

            public void Release() => Releases++;
        }

        // Serves prefabs by location and labelled assets by label, recording every acquisition.
        private sealed class Backend : IContentBackend
        {
            internal readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
            internal readonly Dictionary<string, IList<GameObject>> Labels = new Dictionary<string, IList<GameObject>>();
            internal readonly List<object> Operations = new List<object>();

            public IResourceOperation<T> Acquire<T>(string location, AcquisitionKind kind)
            {
                if (typeof(T) != typeof(GameObject))
                    throw new NotSupportedException("Only prefabs in this backend.");
                var operation = new Operation<GameObject>(Prefabs[location]);
                Operations.Add(operation);
                return (IResourceOperation<T>)(object)operation;
            }

            public IResourceOperation<IList<T>> AcquireLabel<T>(string label)
            {
                var operation = new Operation<IList<GameObject>>(Labels[label]);
                Operations.Add(operation);
                return (IResourceOperation<IList<T>>)(object)operation;
            }
        }

        private GameObject probe;
        private GameObject bare;

        [SetUp]
        public void SetUp()
        {
            probe = new GameObject("Probe", typeof(BoxCollider));
            bare = new GameObject("Bare");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(probe);
            UnityEngine.Object.DestroyImmediate(bare);
        }

        [Test]
        public void ComponentIdsLoadThePrefabAndReturnItsRootComponent()
        {
            var collider = new ResourceId<BoxCollider>(Guid.NewGuid());
            var missing = new ResourceId<Rigidbody>(Guid.NewGuid());
            var backend = new Backend();
            backend.Prefabs["probe"] = probe;
            var provider = new ContentProvider(new ContentCatalog(new[] { ContentEntry.Create(collider, "probe"), ContentEntry.Create(missing, "probe") }), backend);
            var request = provider.LoadAsync(collider).GetAwaiter().GetResult();
            Assert.That(request.Asset, Is.SameAs(probe.GetComponent<BoxCollider>()));
            Assert.That(provider.OutstandingRequests, Is.EqualTo(1));
            request.Release();
            Assert.That(((Operation<GameObject>)backend.Operations[0]).Releases, Is.EqualTo(1), "Releasing the component releases the prefab.");
            var error = Assert.Throws<InvalidOperationException>(() => provider.LoadAsync(missing).GetAwaiter().GetResult());
            StringAssert.Contains("Rigidbody", error.Message);
            Assert.That(provider.OutstandingRequests, Is.Zero, "A prefab without the component is released.");
        }

        [Test]
        public void EachLoadStrategyAcceptsItsTypes()
        {
            var asset = ContentEntry.Create(Guid.NewGuid(), typeof(UnityEngine.Object), "asset");
            var prefab = ContentEntry.Create(Guid.NewGuid(), typeof(GameObject), "prefab");
            Assert.That(asset.Accepts(typeof(Sprite)) && asset.Accepts(typeof(AudioClip)) && asset.Accepts(typeof(ScriptableObject)), Is.True, "An asset loads as any asset type.");
            Assert.That(asset.Accepts(typeof(BoxCollider)), Is.False, "Components come from prefabs, not assets.");
            Assert.That(prefab.Accepts(typeof(GameObject)) && prefab.Accepts(typeof(BoxCollider)), Is.True, "A prefab loads as a GameObject or a root component.");
            Assert.That(prefab.Accepts(typeof(Sprite)), Is.False);
            Assert.That(new ContentAssetId(7).Identity, Is.Not.EqualTo(new ContentPrefabId(7).Identity), "The same value in two strategies is two IDs.");
            Assert.That(new ContentSceneId(0).IsAssigned, Is.False);
        }

        [Test]
        public void LabelsLoadEveryAssetAsOneRequest()
        {
            var backend = new Backend();
            backend.Labels["props"] = new List<GameObject> { probe, bare };
            var provider = new ContentProvider(new ContentCatalog(Array.Empty<ContentEntry>()), backend);
            var all = provider.LoadLabelAsync<GameObject>(new ContentLabel("props")).GetAwaiter().GetResult();
            Assert.That(all.Asset, Is.EqualTo(new[] { probe, bare }));
            all.Release();
            Assert.That(provider.OutstandingRequests, Is.Zero);
            var error = Assert.Throws<InvalidOperationException>(() => provider.LoadLabelAsync<BoxCollider>(new ContentLabel("props")).GetAwaiter().GetResult());
            StringAssert.Contains("Bare", error.Message, "Every labelled prefab must have the component.");
            Assert.That(provider.OutstandingRequests, Is.Zero);
            Assert.Throws<ArgumentException>(() => new ContentLabel(""));
        }
    }
}
