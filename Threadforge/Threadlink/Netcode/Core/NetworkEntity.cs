namespace Threadlink.Netcode
{
    using ECS;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine.Scripting;

    /// <summary>
    /// The fundamental ECS component that links a local entity to the global network state.
    /// </summary>
    [RuntimeComponent, Preserve]
    public struct NetworkEntity : IComponent, IEquatable<NetworkEntity>
    {
        /// <summary>
        /// The GUID assigned by the Host.
        /// A value below 0 indicates an invalid entity, while a value equal to 0 is reserved for RPCs.
        /// </summary>
        public int NetworkID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkEntity(int networkID) => NetworkID = networkID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(NetworkEntity other) => NetworkID == other.NetworkID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object obj) => obj is NetworkEntity other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() => NetworkID.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString() => $"Network Entity [ID: {NetworkID}]";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in NetworkEntity a, in NetworkEntity b) => a.NetworkID == b.NetworkID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in NetworkEntity a, in NetworkEntity b) => a.NetworkID != b.NetworkID;
    }
}