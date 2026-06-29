namespace Threadlink.Utilities.Collections
{
    using System;
    using System.Collections;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

#if UNITY_EDITOR
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Strings;
    using Cysharp.Text;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using Threadlink.Collections;
#endif

    public static class CollectionUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PreventEditorMemoryLeaks(this IDisposable target)
        {
#if UNITY_EDITOR
            void OnPlaymodeExited(PlayModeStateChange change)
            {
                if (change is PlayModeStateChange.ExitingPlayMode)
                {
                    target.Dispose();
                    EditorApplication.playModeStateChanged -= OnPlaymodeExited;
                }
            }

            EditorApplication.playModeStateChanged += OnPlaymodeExited;
#endif
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, NativeArray<T> collection) where T : unmanaged
        {
            return collection.IsCreated && index >= 0 && index < collection.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, INativeList<T> collection) where T : unmanaged
        {
            return !collection.IsEmpty && index >= 0 && index < collection.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeList<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeHashSet<T> target) where T : unmanaged, IEquatable<T>
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeQueue<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<K, V>(ref this UnsafeHashMap<K, V> target)
        where K : unmanaged, IEquatable<K>
        where V : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this NativeArray<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSize<T>(ref this UnsafeList<T> target, int count,
        NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        where T : unmanaged
        {
            if (!target.IsCreated)
                return;

            var length = target.Length;
            target.Resize(Math.Max(count + 1, length + length), options);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Serialize the data in this collection into a binary file, meant to be
        /// stored inside the Unity Project and marked as Addressable.
        /// <para></para>
        /// The binary can then be loaded on any platform using the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
        /// <para></para>
        /// Typically used in conjunction with <see cref="FieldHashMap{TKey, TValue}"/> and <see cref="RefHashMap{TKey, TValue}"/>.
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