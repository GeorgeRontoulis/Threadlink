namespace Threadlink.Utilities.Collections
{
    using System;
    using System.Collections;
    using System.Runtime.CompilerServices;

#if UNITY_EDITOR
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Strings;
    using Cysharp.Text;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
#endif

    public static class CollectionUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this uint index, T collection) where T : ICollection
        {
            return collection != null && index < collection.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this ushort index, T collection) where T : ICollection
        {
            return collection != null && index < collection.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, T collection) where T : ICollection
        {
            return collection != null && index >= 0 && index < collection.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this uint index, ReadOnlySpan<T> collection)
        {
            return collection != null && index < collection.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, ReadOnlySpan<T> span)
        {
            return span != null && index >= 0 && index < span.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this ushort index, ReadOnlySpan<T> span)
        {
            return span != null && index < span.Length;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Serialize the data in this collection into a binary file, meant to be
        /// stored inside the Unity Project and marked as Addressable.
        /// <para></para>
        /// The binary can then be loaded on any platform using the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
        /// <para></para>
        /// Typically used in conjunction with <see cref="global::Threadlink.Collections.FieldTable{K, V}"/> and <see cref="global::Threadlink.Collections.ReferenceTable{K, V}{K, V}"/>.
        /// <para></para>
        /// When used in an authoring context, you are responsible for converting the authoring data into its runtime equivalent.
        /// Use <see cref="Shared.IBinaryAuthor.SerializeAuthoringDataIntoBinary"/> to perform the conversion.
        /// </summary>
        /// <param name="data">The data this table should be converted into, before being serialized. Use your runtime types here.</param>
        /// <param name="filename">The name of the file. Typically the table owner's name (<see cref="ScriptableObject"/> or other).</param>
        public static void SerializeIntoBinary<T>(this ICollection<T> data, DefaultAsset binariesFolder, string filename)
        {
            if (data == null || data.Count <= 0)
            {
                Scribe.Send<Threadlink>("There is no data to serialize!").ToUnityConsole(DebugType.Warning);
                return;
            }

            if (binariesFolder == null)
            {
                data.Send("The ", nameof(binariesFolder), " field has not been assigned!").ToUnityConsole(DebugType.Error);
                return;
            }

            if (string.IsNullOrEmpty(filename))
            {
                data.Send("Please enter a valid ", nameof(filename), "!").ToUnityConsole(DebugType.Error);
                return;
            }

            var projectRelativePath = AssetDatabase.GetAssetPath(binariesFolder);

            if (!AssetDatabase.IsValidFolder(projectRelativePath))
            {
                data.Send("The ", nameof(binariesFolder), "field must target a valid folder inside the project!").ToUnityConsole(DebugType.Error);
                return;
            }

            if (Threadlink.TrySerialize(data, out var serializedData))
            {
                string filePath = Path.Combine(projectRelativePath.ToAbsolutePath(), ZString.Join(string.Empty, filename, ".bytes"));

                File.WriteAllBytes(filePath, serializedData);
                AssetDatabase.ImportAsset(filePath.ToProjectRelativePath(), ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else data.Send("Could not serialize table into binary!").ToUnityConsole(DebugType.Error);
        }
#endif
    }
}