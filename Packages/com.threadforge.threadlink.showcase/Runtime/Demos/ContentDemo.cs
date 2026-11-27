namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Typed content IDs, owner-bound requests and a custom content backend.</summary>
    public sealed class ContentDemo : ShowcaseDemo
    {
        /// <summary>A content backend that "loads" generated textures, standing in for Addressables in this demo.</summary>
        private sealed class GeneratedTextures : IContentBackend
        {
            internal int Loaded;

            public IResourceOperation<T> Acquire<T>(string location, AcquisitionKind kind) => (IResourceOperation<T>)(object)new Operation(this, location);

            private sealed class Operation : IResourceOperation<Texture2D>
            {
                private readonly GeneratedTextures owner;
                private Texture2D texture;

                internal Operation(GeneratedTextures owner, string location)
                {
                    this.owner = owner;
                    texture = new Texture2D(8, 8) { name = location };
                    owner.Loaded++;
                    Completion = UniTask.FromResult(texture);
                }

                public UniTask<Texture2D> Completion { get; }

                public object Handle => texture;

                public void Release()
                {
                    if (texture == null)
                        return;
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    owner.Loaded--;
                }
            }
        }

        // In a project, the Content mapping generates IDs like these (ContentAssets.Group.Name) from ticked Addressables entries.
        private static readonly ContentAssetId Portrait = new ContentAssetId(510010001);
        private static readonly ContentAssetId Missing = new ContentAssetId(510010002);

        private GeneratedTextures backend;
        private ResourceRequest<Texture2D> held;

        public override string Title => "Content";

        public override string Summary =>
            "Content loads by generated IDs: ContentAssets as any asset type, ContentPrefabs as a GameObject or a root component, ContentScenes as scenes. Every load is its own request: release it, or pass an owner and it is released when the owner is destroyed. Addressables is the default backend; any IContentBackend can replace it.";

        public override string[] Packages => new[] { "core" };

        private void Use()
        {
            backend = new GeneratedTextures();
            Content.Map(Portrait, "portrait");
            Content.UseBackend(backend);
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Use();
            var status = Value();
            void Show() => status.text = "Loaded textures: " + backend.Loaded + (held != null && !held.IsReleased ? " (holding " + held.Asset.name + ")" : string.Empty);
            Show();
            panel.Add(status);
            panel.Add(Row(
                Action(context, "Load", async () =>
                {
                    held?.Dispose();
                    held = await Content.LoadAsync<Texture2D>(Portrait);
                    Show();
                }),
                Action(context, "Release", () =>
                {
                    held?.Dispose();
                    Show();
                }),
                Action(context, "Load an unmapped ID", async () =>
                {
                    try
                    {
                        await Content.LoadAsync<Texture2D>(Missing);
                    }
                    catch (Exception error)
                    {
                        context.Log("Refused: " + error.Message);
                    }
                })));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Use();
            var request = await Content.LoadAsync<Texture2D>(Portrait, token);
            Check(request.Asset != null && backend.Loaded == 1, "a mapped ID loads through the backend");
            request.Dispose();
            request.Dispose();
            Check(backend.Loaded == 0, "release is immediate and idempotent");
            var owner = new GameObject("Content owner").AddComponent<OwnerProbe>();
            var owned = await Content.LoadAsync<Texture2D>(Portrait, owner);
            UnityEngine.Object.Destroy(owner.gameObject);
            await UniTask.NextFrame(token);
            Check(owned.IsReleased && backend.Loaded == 0, "an owner's destruction releases its requests");
            bool refused = false;
            try
            {
                await Content.LoadAsync<Texture2D>(Missing, token);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                refused = true;
            }
            Check(refused, "an unmapped ID is refused");
        }

        public override void Dispose()
        {
            held?.Dispose();
            if (Threadlink.IsDeployed)
                Content.UseBackend(null);
        }

        /// <summary>An owner for the demo.</summary>
        public sealed class OwnerProbe : MonoBehaviour { }
    }
}
