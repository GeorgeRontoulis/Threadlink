namespace Threadlink.Utilities.Strings
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Text;
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    public static class StringUtilities
    {
        private static readonly string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/").TrimEnd('/');

        public static string ToAbsolutePath(this string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath))
                throw new ArgumentNullException(nameof(projectRelativePath));

            if (!projectRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path must start with 'Assets/'", nameof(projectRelativePath));

            var sanitized = projectRelativePath.Replace("\\", "/").TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(projectRoot, sanitized);
        }

        public static string ToProjectRelativePath(this string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                throw new ArgumentNullException(nameof(absolutePath));

            var sanitizedAbs = Path.GetFullPath(absolutePath).Replace("\\", "/");

            if (!sanitizedAbs.StartsWith(ZString.Join(string.Empty, projectRoot, "/"), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path is not inside the Unity project", nameof(absolutePath));

            var relative = sanitizedAbs[(projectRoot.Length + 1)..];

            if (!relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path is inside project but not inside the Assets folder", nameof(absolutePath));

            return relative;
        }


        public static void ReadLines<T>(this TextAsset asset, T buffer) where T : ICollection<string>
        {
            if (asset == null || string.IsNullOrEmpty(asset.text))
                return;

            buffer.Clear();

            using var reader = new StringReader(asset.text);
            string line;

            while ((line = reader.ReadLine()) != null)
                buffer.Add(line);
        }

        public static string WithUpperCaseSpacing(this string target)
        {
            if (string.IsNullOrEmpty(target))
                return target;

            using var sb = ZString.CreateStringBuilder(true);
            int length = target.Length;

            sb.Append(target[0]);

            for (int i = 1; i < length; i++)
            {
                var character = target[i];

                if (char.IsUpper(character) && !char.IsWhiteSpace(target[i - 1]))
                    sb.Append(' ');

                sb.Append(character);
            }

            return sb.ToString();
        }

        public static Utf8ValueStringBuilder ToNonAllocText(params object[] input)
        {
            using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
            int length = input.Length;

            for (int i = 0; i < length; i++)
                stringBuilder.Append(input[i]);

            return stringBuilder;
        }

        /// <summary>
        /// Asynchronously load the binary and deserialize its byte data into the specified dictionary.
        /// You are responsible for passing the correct type arguments for proper deserialization.
        /// The loaded binary is automatically unloaded once deserialization is complete, i.e. when this task is done.
        /// </summary>
        /// <typeparam name="K">The type of key.</typeparam>
        /// <typeparam name="V">The type of value.</typeparam>
        /// <param name="binaryReference">The <see cref="AssetReference"/> to the binary.</param>
        /// <returns>The reconstructed dictionary after deserialization.</returns>
        public static async UniTask<Dictionary<K, V>> DeserializeIntoDictionaryAsync<K, V>(this AssetReferenceT<TextAsset> binaryReference)
        {
            var binary = await Threadlink.LoadAssetAsync<TextAsset>(binaryReference);

            if (binary == null)
                binaryReference.Send("Binary is NULL!").ToUnityConsole(DebugType.Error);

            if (!Threadlink.TryDeserialize(binary.bytes, out Dictionary<K, V> result))
                binaryReference.Send("Could not deserialize binary!").ToUnityConsole(DebugType.Error);

            binaryReference.ReleaseAsset();
            return result;
        }
    }
}
