namespace Threadlink.Utilities.Collections
{
    using System;
    using System.Collections;
    using System.Runtime.CompilerServices;

    public static class CollectionUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear<T>(ref T[] array, bool nullifyCollection = true)
        {
            if (array != null)
            {
                Array.Clear(array, 0, array.Length);

                if (nullifyCollection)
                    array = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetLastIndex<T>(this T collection, out int index) where T : ICollection
        {
            if (collection == null)
            {
                index = -1;
                return false;
            }

            index = collection.Count - 1;
            return index.IsWithinBoundsOf(collection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, T collection) where T : ICollection
        {
            return collection != null && index >= 0 && index < collection.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, ReadOnlySpan<T> span)
        {
            return span != null && index >= 0 && index < span.Length;
        }
    }
}