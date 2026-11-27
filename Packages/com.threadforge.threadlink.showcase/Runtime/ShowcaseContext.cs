namespace Threadforge.Threadlink.Showcase
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>What the gallery gives every demo: a log and a scene object to attach to.</summary>
    public sealed class ShowcaseContext : IDisposable
    {
        private readonly List<string> lines = new List<string>();

        /// <summary>Creates a context whose demos attach components and children to <paramref name="host"/>.</summary>
        public ShowcaseContext(GameObject host) => Host = host ? host : throw new ArgumentNullException(nameof(host));

        /// <summary>Scene object demos attach components and child objects to.</summary>
        public GameObject Host { get; }

        /// <summary>Raised for every log line.</summary>
        public event Action<string> Logged;

        /// <summary>The most recent log lines, oldest first.</summary>
        public IReadOnlyList<string> Lines => lines;

        /// <summary>Adds a log line.</summary>
        public void Log(string line)
        {
            lines.Add(line);
            if (lines.Count > 200)
                lines.RemoveAt(0);
            Logged?.Invoke(line);
        }

        /// <summary>Runs one gallery action as an operation in the session log (see the Runtime monitor) and logs failures.</summary>
        public async void Run(string name, Func<UniTask> action)
        {
            var operation = Threadlink.Begin("Showcase", name);
            try
            {
                await action();
                operation.Succeed();
            }
            catch (OperationCanceledException)
            {
                operation.Cancel();
                Log(name + ": cancelled.");
            }
            catch (Exception error)
            {
                operation.Fail(error);
                Log(name + " failed: " + error.Message);
            }
        }

        /// <inheritdoc />
        public void Dispose() => Logged = null;
    }
}
