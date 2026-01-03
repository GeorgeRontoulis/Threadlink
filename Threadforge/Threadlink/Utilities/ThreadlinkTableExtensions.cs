namespace Threadlink.Collections.Extensions
{
    using Utilities.Collections;

    public static class ThreadlinkTableExtensions
    {
        public static bool TryGetValue<K, V>(this FieldTable<K, V> table, K key, out V value)
        {
            int index = table.IndexOf(key);
            var entries = table.Entries;

            if (index.IsWithinBoundsOf(entries))
            {
                value = entries[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        internal static int IndexOf<K, V>(this FieldTable<K, V> table, K key)
        {
            var entries = table.Entries;
            int count = entries.Length;

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];

                if (table.KeyComparer.Equals(entry.Key, key))
                    return i;
            }

            return -1;
        }

        public static bool TryGetValue<K, V>(this ReferenceTable<K, V> table, K key, out V value)
        {
            int index = table.IndexOf(key);
            var entries = table.Entries;

            if (index.IsWithinBoundsOf(entries))
            {
                value = entries[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        internal static int IndexOf<K, V>(this ReferenceTable<K, V> table, K key)
        {
            var entries = table.Entries;
            int count = entries.Length;

            for (int i = 0; i < count; i++)
            {
                if (table.KeyComparer.Equals(entries[i].Key, key))
                    return i;
            }

            return -1;
        }
    }
}