namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using UnityEngine;

    /// <summary>Runs an awaitable test body as a Unity test coroutine.</summary>
    internal static class AwaitableTest
    {
        internal static IEnumerator Run(Func<UniTask> body)
        {
            var running = body();
            while (!running.Status.IsCompleted())
                yield return null;
            running.GetAwaiter().GetResult();
        }
    }
}
