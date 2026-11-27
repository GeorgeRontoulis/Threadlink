namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    /// <summary>Addressables scene residency through ordinary tracked content requests.</summary>
    internal sealed class ContentSceneBackend : ISceneBackend
    {
        private sealed class Residency : ISceneResidency
        {
            private readonly ResourceRequest<SceneInstance> request;
            private readonly GameObject[] roots;
            private readonly bool[] activeOnPresentation;

            internal Residency(ResourceRequest<SceneInstance> request, bool activateInactiveRoots)
            {
                this.request = request;
                roots = request.Asset.Scene.GetRootGameObjects();
                activeOnPresentation = new bool[roots.Length];
                for (int i = 0; i < roots.Length; i++)
                    activeOnPresentation[i] = activateInactiveRoots || roots[i].activeSelf;
            }

            public Scene Scene => request.Asset.Scene;
            public bool IsLoaded => !request.IsReleased && request.Asset.Scene.isLoaded;

            public void SetPresented(bool presented)
            {
                if (!IsLoaded)
                    throw new InvalidOperationException("Scene was externally unloaded.");
                for (int i = 0; i < roots.Length; i++)
                    if (roots[i] != null)
                        roots[i].SetActive(presented && activeOnPresentation[i]);
            }

            public UniTask ReleaseAsync() => request.ReleaseAsync();
        }

        private readonly Func<ContentProvider> content;
        private readonly bool activateInactiveRoots;

        /// <summary>Uses an explicitly supplied content provider; no service host is required.</summary>
        public ContentSceneBackend(Func<ContentProvider> content, bool activateInactiveRoots = false)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.activateInactiveRoots = activateInactiveRoots;
        }

        /// <summary>Loads a scene and hides its roots until presentation. Author inactive roots to prevent any pre-completion visibility.</summary>
        public async UniTask<ISceneResidency> LoadAsync(Guid scene, CancellationToken token)
        {
            var request = await content().AcquireAsync(new ResourceId<SceneInstance>(scene), AcquisitionKind.Scene, token);
            try
            {
                var result = new Residency(request, activateInactiveRoots);
                result.SetPresented(false);
                return result;
            }
            catch
            {
                await request.ReleaseAsync();
                throw;
            }
        }
    }
}
