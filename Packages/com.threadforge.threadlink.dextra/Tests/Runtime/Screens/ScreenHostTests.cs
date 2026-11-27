namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public class ScreenHostTests
    {
        private sealed class View : IScreenView
        {
            internal bool Visible,
                Focused,
                FailPrepare;
            internal int Disposed;
            internal UniTask? Pending;

            public UniTask PrepareAsync(CancellationToken token) =>
                FailPrepare
                    ? UniTask.FromException(new Exception("prepare"))
                    : Pending ?? UniTask.CompletedTask;

            public void SetVisible(bool visible)
            {
                Visible = visible;
            }

            public void SetFocused(bool focused)
            {
                Focused = focused;
            }

            public void Dispose()
            {
                Disposed++;
                Visible = Focused = false;
            }
        }

        private sealed class Factory : IScreenFactory
        {
            internal View Next;

            public UniTask<IScreenView> CreateAsync(Guid id, CancellationToken token) =>
                UniTask.FromResult<IScreenView>(Next);
        }

        [Test]
        public void FailedOpenPreservesPreviousUsableScreen()
        {
            var factory = new Factory { Next = new View() };
            using var host = new ScreenHost(factory);
            var first = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu).GetAwaiter().GetResult();
            var usable = factory.Next;
            factory.Next = new View { FailPrepare = true };
            var failed = factory.Next;
            Assert.That(TaskAssert.Faulted(host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu)), Is.True);
            Assert.That(usable.Visible && usable.Focused, Is.True);
            Assert.That(failed.Disposed, Is.EqualTo(1));
            host.CloseAsync(first).GetAwaiter().GetResult();
            Assert.That(usable.Disposed, Is.EqualTo(1));
        }

        [Test]
        public void LayersAndMultipleInstancesRestoreRemainingFocus()
        {
            var factory = new Factory { Next = new View() };
            using var host = new ScreenHost(factory);
            var first = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu).GetAwaiter().GetResult();
            var firstView = factory.Next;
            factory.Next = new View();
            var second = host.OpenAsync(first.Definition, ScreenLayer.Menu).GetAwaiter().GetResult();
            var secondView = factory.Next;
            factory.Next = new View();
            var hud = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Hud, takesFocus: false).GetAwaiter().GetResult();
            Assert.That(firstView.Visible, Is.False);
            Assert.That(secondView.Visible && secondView.Focused, Is.True);
            host.CloseAsync(second).GetAwaiter().GetResult();
            Assert.That(firstView.Visible && firstView.Focused, Is.True);
            Assert.That(host.OpenCount, Is.EqualTo(2));
            host.CloseAsync(hud).GetAwaiter().GetResult();
            host.CloseAsync(first).GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator DestructionImmediatelyCleansPreparingViewAndLateCompletionCannotCommit()
        {
            var gate = new UniTaskCompletionSource<bool>();
            var view = new View { Pending = gate.Task };
            var factory = new Factory { Next = view };
            var host = new ScreenHost(factory);
            var opening = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Modal);
            host.Dispose();
            Assert.That(view.Disposed, Is.EqualTo(1));
            gate.TrySetResult(true);
            for (int i = 0; i < 120 && !opening.Status.IsCompleted(); i++)
                yield return null;
            Assert.That(opening.Status.IsCanceled() || TaskAssert.Faulted(opening), Is.True);
            Assert.That(host.OpenCount, Is.Zero);
            Assert.That(view.Disposed, Is.EqualTo(1));
        }

        [Test]
        public void CachingIsExplicitAndReportsRetainedViewsAfterClose()
        {
            var view = new View();
            var source = new Factory { Next = view };
            using var cache = new ScreenCache(source, 1);
            var id = Guid.NewGuid();
            cache.PrewarmAsync(id).GetAwaiter().GetResult();
            Assert.That(cache.RetainedViews, Is.EqualTo(1));
            Assert.That(cache.ActiveLeases, Is.Zero);
            using var host = new ScreenHost(cache);
            var screen = host.OpenAsync(id, ScreenLayer.Menu).GetAwaiter().GetResult();
            host.CloseAsync(screen).GetAwaiter().GetResult();
            Assert.That(view.Disposed, Is.Zero);
            cache.Dispose();
            Assert.That(view.Disposed, Is.EqualTo(1));
            Assert.That(cache.RetainedViews, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ClosingHostDuringPreparationEvictsCachedView()
        {
            var gate = new UniTaskCompletionSource<bool>();
            var view = new View { Pending = gate.Task };
            var factory = new Factory { Next = view };
            using var cache = new ScreenCache(factory, 1);
            var host = new ScreenHost(cache);
            var opening = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu);
            host.Dispose();
            Assert.That(cache.RetainedViews, Is.Zero);
            Assert.That(view.Disposed, Is.EqualTo(1));
            gate.TrySetResult(true);
            for (int i = 0; i < 120 && !opening.Status.IsCompleted(); i++)
                yield return null;
            Assert.That(opening.Status.IsCanceled() || TaskAssert.Faulted(opening), Is.True);
            Assert.That(view.Disposed, Is.EqualTo(1));
        }
    }
}
