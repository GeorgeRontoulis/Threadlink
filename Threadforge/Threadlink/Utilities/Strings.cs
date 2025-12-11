namespace Threadlink.Utilities.Strings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    public static class Strings
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
    }
}
