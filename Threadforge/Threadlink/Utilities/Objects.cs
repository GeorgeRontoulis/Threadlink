namespace Threadlink.Utilities.Objects
{
    using Core;
    using UnityEngine;

    public static class ThreadlinkObjectUtilities
    {
        public static T Clone<T>(this T original) where T : LinkableAsset
        {
            var copy = Object.Instantiate(original);

            copy.name = original.name;
            copy.IsInstance = true;

            return copy;
        }

        public static bool As<T>(this GameObject target, out T component) where T : Component
        {
            if (target == null)
            {
                component = null;
                return false;
            }

            return target.TryGetComponent(out component);
        }
    }
}