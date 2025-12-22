#if UNITY_EDITOR
namespace Threadlink.Authoring
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Text;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Collections;
    using Utilities.Strings;

    [Serializable]
    public sealed class ThreadlinkSerializableAuthoringTable<K, V> : ThreadlinkAuthoringTable<K, V>
    {
        [SerializeField] private DefaultAsset binariesFolder = null;

        /// <summary>
        /// Serialize this table into a binary file, meant to be
        /// stored inside the Unity Project and marked as Addressable. 
        /// <para></para>
        /// The binary can then be loaded on any platform using the
        /// <see cref="UnityEngine.AddressableAssets"/> Pipeline.
        /// <para></para>
        /// You are responsible for converting the authoring data into its runtime equivalent.
        /// Use <see cref="Shared.IBinaryAuthor.SerializeAuthoringDataIntoBinary"/> to perform the conversion.
        /// </summary>
        /// <param name="serializableRuntimeData">The data this table should be converted into, before being serialized. Use your runtime types here.</param>
        /// <param name="filename">The name of the file. Typically the table owner's name (<see cref="ScriptableObject"/> or other).</param>
        public void SerializeIntoBinary<Key, Value>(Dictionary<Key, Value> serializableRuntimeData, string filename)
        {
            if (serializableRuntimeData == null || serializableRuntimeData.Count <= 0)
            {
                this.Send("There is no data to serialize!").ToUnityConsole(DebugType.Warning);
                return;
            }

            if (binariesFolder == null)
            {
                this.Send("The ", nameof(binariesFolder), " field has not been assigned!").ToUnityConsole(DebugType.Error);
                return;
            }

            if (string.IsNullOrEmpty(filename))
            {
                this.Send("Please enter a valid ", nameof(filename), "!").ToUnityConsole(DebugType.Error);
                return;
            }

            var projectRelativePath = AssetDatabase.GetAssetPath(binariesFolder);

            if (!AssetDatabase.IsValidFolder(projectRelativePath))
            {
                this.Send("The ", nameof(binariesFolder), "field must target a valid folder inside the project!").ToUnityConsole(DebugType.Error);
                return;
            }

            if (Threadlink.TrySerialize(serializableRuntimeData, out var serializedData))
            {
                File.WriteAllBytes(Path.Combine(projectRelativePath.ToAbsolutePath(), ZString.Join(string.Empty, filename, ".bytes")), serializedData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else this.Send("Could not serialize table into binary!").ToUnityConsole(DebugType.Error);
        }
    }

    [Serializable]
    public class ThreadlinkAuthoringTable<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] internal K key = default;
            [SerializeField] internal V value = default;

            internal Entry(K key, V value)
            {
                this.key = key;
                this.value = value;
            }
        }

        public int Count => entries.Count;

        [SerializeField] private List<Entry> entries = new();

        public void Add(K key, V value)
        {
            int index = FindIndex(key);

            if (index.IsWithinBoundsOf(entries))
            {
                // Overwrite existing entry (Dictionary semantics)
                entries[index].value = value;
                return;
            }

            entries.Add(new Entry(key, value));
        }

        public bool Remove(K key)
        {
            int index = FindIndex(key);

            if (!index.IsWithinBoundsOf(entries))
                return false;

            entries.RemoveAt(index);
            return true;
        }

        public bool TryGetValue(K key, out V value)
        {
            int index = FindIndex(key);

            if (index.IsWithinBoundsOf(entries))
            {
                value = entries[index].value;
                return true;
            }

            value = default;
            return false;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                yield return new KeyValuePair<K, V>(entry.key, entry.value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindIndex(K key)
        {
            var comparer = EqualityComparer<K>.Default;

            for (int i = 0; i < entries.Count; i++)
            {
                if (comparer.Equals(entries[i].key, key))
                    return i;
            }

            return -1;
        }
    }
}
#endif