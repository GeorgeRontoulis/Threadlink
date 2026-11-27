namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;

    /// <summary>Screen creation through native content instance acquisitions, with an explicit renderer adapter.</summary>
    internal sealed class ContentScreenFactory : IScreenFactory
    {
        private readonly Func<ContentProvider> content;
        private readonly Func<GameObject, IScreenView> adapter;

        /// <summary>Creates a factory without requiring a particular renderer, audio or input backend.</summary>
        public ContentScreenFactory(Func<ContentProvider> content, Func<GameObject, IScreenView> adapter)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        private sealed class View : IScreenView, IScreenObject
        {
            private readonly IScreenView view;
            private ResourceRequest<GameObject> request;

            internal View(IScreenView view, ResourceRequest<GameObject> request)
            {
                this.view = view;
                this.request = request;
            }

            public UniTask PrepareAsync(CancellationToken token) => view.PrepareAsync(token);

            public UnityEngine.GameObject Instance => request != null && !request.IsReleased ? request.Asset : null;

            public void SetVisible(bool visible) => view.SetVisible(visible);

            public void SetFocused(bool focused) => view.SetFocused(focused);

            public void Dispose()
            {
                if (request == null)
                    return;
                var release = request;
                request = null;
                try
                {
                    view.Dispose();
                }
                finally
                {
                    release.Release();
                }
            }
        }

        /// <summary>Creates one independently owned prefab instance and transfers it to the screen host.</summary>
        public async UniTask<IScreenView> CreateAsync(Guid screen, CancellationToken token)
        {
            var request = await content().AcquireAsync(new ResourceId<GameObject>(screen), AcquisitionKind.Instance, token);
            try
            {
                return new View(
                    adapter(request.Asset)
                        ?? throw new InvalidOperationException(
                            "No screen view exists on this prefab."
                        ),
                    request
                );
            }
            catch
            {
                request.Release();
                throw;
            }
        }
    }
}
