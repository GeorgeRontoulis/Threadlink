namespace Threadforge.Threadlink.Tests
{
    using System;
    using Cysharp.Threading.Tasks;

    /// <summary>Assertions on UniTask outcomes that also observe failures, so they are never reported later as unobserved.</summary>
    internal static class TaskAssert
    {
        /// <summary>Whether the task has failed. Observes the failure.</summary>
        internal static bool Faulted(UniTask task)
        {
            if (!task.Status.IsFaulted())
                return false;
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Observed.
            }
            return true;
        }

        /// <summary>Whether the task has failed. Observes the failure.</summary>
        internal static bool Faulted<T>(UniTask<T> task) => Faulted(task.AsUniTask());
    }
}
