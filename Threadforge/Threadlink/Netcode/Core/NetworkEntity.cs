namespace Threadlink.Netcode
{
    using ECS;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The fundamental ECS component that links a local entity to the global network state.
    /// </summary>
    public struct NetworkEntity : IComponent, IEquatable<NetworkEntity>
    {
        /// <summary>
        /// The globally unique identifier assigned by the Host.
        /// A value below 0 indicates an invalid entity.
        /// </summary>
        public int NetworkID;

        /// <summary>
        /// True if this local machine is responsible for simulating and broadcasting this entity's state.
        /// False if this machine must wait for network packets to update this entity's state.
        /// </summary>
        public bool BelongsToHost;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkEntity(int networkID, bool belongsToHost)
        {
            NetworkID = networkID;
            BelongsToHost = belongsToHost;
        }

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