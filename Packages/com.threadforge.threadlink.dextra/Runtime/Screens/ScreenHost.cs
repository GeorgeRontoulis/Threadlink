namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>Independent presentation layers.</summary>
    public enum ScreenLayer
    {
        Hud,
        Overlay,
        Menu,
        Modal,
    }

    /// <summary>Receives a screen's presentation changes. Implement it on components of a screen's GameObject.</summary>
    public interface IScreenListener
    {
        /// <summary>The screen became visible or hidden.</summary>
        void OnScreenVisible(bool visible);

        /// <summary>The screen gained or lost input focus.</summary>
        void OnScreenFocused(bool focused);
    }

    internal static class ScreenListeners
    {
        private static readonly System.Collections.Generic.List<IScreenListener> found = new System.Collections.Generic.List<IScreenListener>();

        internal static void Visible(UnityEngine.GameObject screen, bool visible)
        {
            screen.GetComponents(found);
            foreach (var listener in found)
                listener.OnScreenVisible(visible);
            found.Clear();
        }

        internal static void Focused(UnityEngine.GameObject screen, bool focused)
        {
            screen.GetComponents(found);
            foreach (var listener in found)
                listener.OnScreenFocused(focused);
            found.Clear();
        }
    }

    /// <summary>A view that knows its GameObject.</summary>
    internal interface IScreenObject
    {
        UnityEngine.GameObject Instance { get; }
    }

    /// <summary>Renderer-independent view owned by a screen handle.</summary>
    public interface IScreenView : IDisposable
    {
        /// <summary>Awaits required preparation before presentation commitment.</summary>
        UniTask PrepareAsync(CancellationToken token);

        /// <summary>Changes visibility without releasing resources.</summary>
        void SetVisible(bool visible);

        /// <summary>Changes input focus; implementations may preserve their previous selected control.</summary>
        void SetFocused(bool focused);
    }

    /// <summary>Explicit screen acquisition; returned views transfer cleanup ownership to the host.</summary>
    internal interface IScreenFactory
    {
        /// <summary>Creates one view on demand. Failures must clean partial acquisitions.</summary>
        UniTask<IScreenView> CreateAsync(Guid screen, CancellationToken token);
    }

    /// <summary>Optional input integration; claims compose with other gameplay/menu claims.</summary>
    internal interface IInputBlocker
    {
        /// <summary>Acquires one independently disposable input claim.</summary>
        IDisposable Claim(ScreenLayer layer);
    }

    /// <summary>One screen instance. Multiple instances of the same definition are supported.</summary>
    public sealed class ScreenHandle
    {
        internal IScreenView View;
        internal IDisposable Input;

        /// <summary>The screen's GameObject, or null for views without one.</summary>
        public UnityEngine.GameObject Instance { get; internal set; }

        /// <summary>The root element of a UI Toolkit screen, for binding its controls; null for other screens.</summary>
        public UnityEngine.UIElements.VisualElement Root =>
            Instance != null && Instance.TryGetComponent<UnityEngine.UIElements.UIDocument>(out var document) ? document.rootVisualElement : null;
        internal bool Focus;

        /// <summary>Definition identity.</summary>
        public Guid Definition { get; internal set; }

        /// <summary>Host layer.</summary>
        public ScreenLayer Layer { get; internal set; }

        /// <summary>Whether the host has closed this instance.</summary>
        public bool IsClosed { get; internal set; }
    }

    /// <summary>Serialized on-demand screen operations with rollback, layered focus and immediate destruction cleanup.</summary>
    /// <remarks>Use on one thread/synchronization context. The default policy releases on close; no hidden view cache exists.</remarks>
    internal sealed class ScreenHost : IDisposable
    {
        private sealed class OwnedView : IScreenView, IScreenObject
        {
            private IScreenView view;

            internal OwnedView(IScreenView view) => this.view = view;

            public UnityEngine.GameObject Instance => (view as IScreenObject)?.Instance;

            public UniTask PrepareAsync(CancellationToken token) => view.PrepareAsync(token);

            public void SetVisible(bool visible) => view?.SetVisible(visible);

            public void SetFocused(bool focused) => view?.SetFocused(focused);

            public void Dispose()
            {
                var release = view;
                view = null;
                release?.Dispose();
            }
        }

        private readonly IScreenFactory factory;
        private readonly IInputBlocker input;
        private readonly List<ScreenHandle> screens = new List<ScreenHandle>();
        private readonly SemaphoreSlim navigation = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;
        private IScreenView preparing;

        /// <summary>Number of currently retained screen instances.</summary>
        public int OpenCount => screens.Count;

        /// <summary>Called after a screen is presented.</summary>
        internal Action<ScreenHandle> Opened;

        /// <summary>Called after a screen is closed and cleaned up, including when the host is disposed.</summary>
        internal Action<ScreenHandle> Closed;

        /// <summary>Creates an independent host; input blocking is an explicit optional integration.</summary>
        public ScreenHost(IScreenFactory factory, IInputBlocker input = null)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.input = input;
        }

        /// <summary>Acquires/prepares before changing the previous UI. Retains no failed or abandoned view.</summary>
        public async UniTask<ScreenHandle> OpenAsync(
            Guid definition,
            ScreenLayer layer,
            bool takesFocus = true,
            bool blocksInput = false,
            CancellationToken token = default
        )
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ScreenHost));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                lifetime.Token
            );
            await navigation.WaitAsync(linked.Token);
            IScreenView view = null;
            IDisposable claim = null;
            ScreenHandle handle = null;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                view = new OwnedView(
                    await factory.CreateAsync(definition, linked.Token)
                        ?? throw new InvalidOperationException("Screen factory returned no view.")
                );
                preparing = view;
                linked.Token.ThrowIfCancellationRequested();
                view.SetVisible(false);
                await view.PrepareAsync(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (disposed)
                    throw new ObjectDisposedException(nameof(ScreenHost));
                if (blocksInput)
                {
                    if (input == null)
                        throw new NotSupportedException("No input policy is selected.");
                    claim = input.Claim(layer);
                }
                handle = new ScreenHandle
                {
                    Definition = definition,
                    Layer = layer,
                    Focus = takesFocus,
                    View = view,
                    Input = claim,
                    Instance = (view as IScreenObject)?.Instance,
                };
                screens.Add(handle);
                try
                {
                    RefreshPresentation();
                }
                catch
                {
                    screens.Remove(handle);
                    RefreshPresentation();
                    throw;
                }
                preparing = null;
                view = null;
                claim = null;
                Opened?.Invoke(handle);
                return handle;
            }
            finally
            {
                try
                {
                    preparing = null;
                    try
                    {
                        claim?.Dispose();
                    }
                    finally
                    {
                        view?.Dispose();
                    }
                }
                finally
                {
                    navigation.Release();
                }
            }
        }

        /// <summary>Closes one instance and restores remaining layer/focus/input claims.</summary>
        public async UniTask CloseAsync(ScreenHandle handle, CancellationToken token = default)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));
            if (handle.IsClosed)
                return;
            if (disposed)
                throw new ObjectDisposedException(nameof(ScreenHost));
            await navigation.WaitAsync(token);
            try
            {
                if (handle.IsClosed)
                    return;
                if (!screens.Remove(handle))
                    throw new ArgumentException("Screen belongs to another host.");
                handle.IsClosed = true;
                var errors = new List<Exception>();
                try
                {
                    handle.Input?.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    handle.View.SetFocused(false);
                    handle.View.SetVisible(false);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    handle.View.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                handle.View = null;
                handle.Input = null;
                try
                {
                    if (!disposed)
                        RefreshPresentation();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    Closed?.Invoke(handle);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                if (errors.Count > 0)
                    throw new AggregateException("Screen closed with cleanup failures.", errors);
            }
            finally
            {
                navigation.Release();
            }
        }

        private void RefreshPresentation()
        {
            ScreenHandle focused = null;
            foreach (var screen in screens)
                if (screen.Focus && (focused == null || screen.Layer >= focused.Layer))
                    focused = screen;
            foreach (var screen in screens)
            {
                bool visible = true;
                if (screen.Layer == ScreenLayer.Menu || screen.Layer == ScreenLayer.Modal)
                    for (int i = screens.IndexOf(screen) + 1; i < screens.Count; i++)
                        if (screens[i].Layer == screen.Layer)
                        {
                            visible = false;
                            break;
                        }
                screen.View.SetFocused(false);
                screen.View.SetVisible(visible);
            }
            focused?.View.SetFocused(true);
        }

        /// <summary>Cancels pending work and immediately cleans committed views/claims. Late factory results are cleaned on return.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            var errors = new List<Exception>();
            try
            {
                lifetime.Cancel();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            try
            {
                preparing?.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            preparing = null;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                var screen = screens[i];
                screen.IsClosed = true;
                try
                {
                    screen.Input?.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    screen.View.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    Closed?.Invoke(screen);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                screen.Input = null;
                screen.View = null;
            }
            screens.Clear();
            if (errors.Count > 0)
                throw new AggregateException(errors);
        }
    }
}
