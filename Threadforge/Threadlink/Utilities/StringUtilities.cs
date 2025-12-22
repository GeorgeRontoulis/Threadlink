namespace Threadlink.Utilities.Strings
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Text;
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Threadlink.Addressables;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    public static class StringUtilities
    {
        private static readonly string separator = Path.DirectorySeparatorChar.ToString();
        private static readonly string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        public static string ToAbsolutePath(this string projectRelativePath)
        {
            if (!projectRelativePath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path must start with 'Assets'", nameof(projectRelativePath));

            var sanitizedPath = projectRelativePath.Replace("\\", "/").TrimStart('/');

            return Path.Combine(projectRoot, sanitizedPath.Replace("/", separator));
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
            if (binaryReference == null || !binaryReference.RuntimeKeyIsValid())
            {
                Scribe.Send<Threadlink>("Binary referece is invalid!").ToUnityConsole(DebugType.Error);
                return null;
            }

            var runtimeKey = binaryReference.RuntimeKey;
            var binaryFile = await Threadlink.LoadAssetAsync<TextAsset>(runtimeKey);
            Dictionary<K, V> result = null;

            ///Note: Probably keep it this way but include async API for deserialization as well.
            if (binaryFile == null || !Threadlink.TryDeserialize(binaryFile.bytes, out result))
                Scribe.Send<Threadlink>("Could not deserialize binary!").ToUnityConsole(DebugType.Error);

            ThreadlinkResourceProvider<TextAsset>.ReleaseAt(runtimeKey);
            return result;
        }
    }
}
