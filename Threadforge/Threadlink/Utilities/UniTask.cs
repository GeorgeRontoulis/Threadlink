namespace Threadlink.Utilities.UniTask
{
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public static class UniTaskUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask AwaitAllThenClear(this List<UniTask> tasks, bool trim = false)
        {
            await UniTask.WhenAll(tasks);

            tasks.Clear();

            if (trim)
                tasks.TrimExcess();
        }
    }
}
