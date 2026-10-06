namespace Threadlink.Core.NativeSubsystems.Iris
{
    using System;
    using System.Runtime.CompilerServices;

    internal interface IClearable { void Clear(); }
    internal interface IDelegateList : IClearable { int Count { get; } }

    internal sealed class DelegateList<T> : IDelegateList where T : Delegate
    {
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        internal T[] slots = new T[2];
        private int count;
        private int extent;
        private int dispatchDepth;

        // Dispatch captures an upper bound. Tombstones keep removals stable through nested publications;
        // additions are visible to a nested publication, but never to the publication already in progress.
        internal int BeginDispatch() { dispatchDepth++; return extent; }
        internal void EndDispatch()
        {
            if (--dispatchDepth != 0) return;
            int written = 0;
            for (int i = 0; i < extent; i++)
                if (slots[i] != null) slots[written++] = slots[i];
            Array.Clear(slots, written, extent - written);
            extent = written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(T d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            int length = slots.Length;

            if (extent == length)
                Array.Resize(ref slots, Math.Max(2, length + length));

            slots[extent++] = d;
            count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Remove(T d)
        {
            if (d == null) return false;
            for (int i = 0; i < extent; i++)
            {
                ref var slot = ref slots[i];

                if (slot == d)
                {
                    slot = null;
                    count--;
                    if (dispatchDepth == 0)
                    {
                        Array.Copy(slots, i + 1, slots, i, extent - i - 1);
                        slots[--extent] = null;
                    }
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Contains(T d)
        {
            if (d == null) return false;
            for (int i = 0; i < extent; i++)
                if (slots[i] == d) return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(slots, 0, extent);
            count = 0;
            if (dispatchDepth == 0) extent = 0;
        }
    }
}
