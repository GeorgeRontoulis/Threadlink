namespace Threadlink.Core
{
    using NativeSubsystems.Iris;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Dealing with <see cref="UnityEngine.LowLevel.PlayerLoop"/>
    /// to provide a centralized update point would be overkill,
    /// so we have this instead.
    /// </summary>
    internal sealed class ThreadlinkLoop : MonoBehaviour
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Update() => Iris.Publish(ThreadlinkIDs.Iris.Events.OnUpdate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixedUpdate() => Iris.Publish(ThreadlinkIDs.Iris.Events.OnFixedUpdate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LateUpdate() => Iris.Publish(ThreadlinkIDs.Iris.Events.OnLateUpdate);
    }
}
