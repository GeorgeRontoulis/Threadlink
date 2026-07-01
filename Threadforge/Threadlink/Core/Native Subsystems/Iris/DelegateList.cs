namespace Threadlink.Core.NativeSubsystems.Iris
{
    using System;
    using System.Runtime.CompilerServices;

    internal interface IClearable { void Clear(); }
    internal interface IDelegateList : IClearable { int Count { get; } }

    /// <summary>
    /// A flat, array-backed list of delegates of type <typeparamref name="T"/>.
    /// Replaces multicast delegate chaining (Delegate.Combine / Delegate.Remove),
    /// which allocates a new delegate object on every subscription change.
    /// <para/>
    /// This type has no public surface — it lives entirely inside Iris as the
    /// backing store for each event slot.
    /// </summary>
    internal sealed class DelegateList<T> : IDelegateList where T : Delegate
    {
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        internal T[] slots = new T[2];
        private int count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(T d)
        {
            int length = slots.Length;

            if (count == length)
                Array.Resize(ref slots, Math.Max(2, length + length));

            slots[count++] = d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Remove(T d)
        {
            for (int i = 0; i < count; i++)
            {
                ref var slot = ref slots[i];

                if (slot == d)
                {
                    slot = slots[--count];
                    slots[count] = null;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Contains(T d)
        {
            for (int i = 0; i < count; i++)
                if (slots[i] == d) return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(slots, 0, count);
            count = 0;
        }
    }
}