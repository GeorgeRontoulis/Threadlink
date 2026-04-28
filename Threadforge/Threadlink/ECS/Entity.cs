namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;

    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly int ID;
        public readonly int Generation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity(int id, int gen)
        {
            ID = id;
            Generation = gen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) => ID == other.ID && Generation == other.Generation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is Entity other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (ID * 397) ^ Generation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"Entity [ID: {ID} | Gen: {Generation}]";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Entity a, in Entity b) => a.ID == b.ID && a.Generation == b.Generation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Entity a, in Entity b) => a.ID != b.ID || a.Generation != b.Generation;
    }
}