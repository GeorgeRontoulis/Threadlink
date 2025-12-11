namespace Threadlink.Utilities.Objects
{
    using Threadlink.Core;

    public static class LinkableAssetExtensions
    {
        public static T Clone<T>(this T original) where T : LinkableAsset
        {
            var copy = UnityEngine.Object.Instantiate(original);

            copy.name = original.name;
            copy.IsInstance = true;

            return copy;
        }
    }
}