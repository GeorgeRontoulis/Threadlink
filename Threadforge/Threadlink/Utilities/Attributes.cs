namespace Threadlink.Utilities.Attributes
{
    using System;
    using UnityEngine;

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinMaxRangeAttribute : PropertyAttribute
    {
        #region Fields
        public readonly float MinLimit;
        public readonly float MaxLimit;
        public readonly uint Decimals;
        #endregion

        #region Setup
        /// <summary>
        /// A bounded range for integers.
        /// </summary>
        /// <param name="minLimit">The minimum acceptable value.</param>
        /// <param name="maxLimit">The maximum acceptable value.</param>
        public MinMaxRangeAttribute(int minLimit, int maxLimit)
        {
            MinLimit = minLimit;
            MaxLimit = maxLimit;
        }

        /// <summary>
        /// A bounded range for floats.
        /// </summary>
        /// <param name="minLimit">The minimum acceptable value.</param>
        /// <param name="maxLimit">The maximum acceptable value.</param>
        /// <param name="decimals">How many decimals the inspector labels should display. Values must be in the [0,3]
        /// range. Default is 1.</param>
        public MinMaxRangeAttribute(float minLimit, float maxLimit, uint decimals = 1)
        {
            MinLimit = minLimit;
            MaxLimit = maxLimit;
            Decimals = decimals;
        }
        #endregion
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ReadOnlyAttribute : PropertyAttribute { }

    public enum HashMapDrawerMode : byte
    {
        /// <summary>
        /// Odin's drawer when Odin is installed, the native drawer otherwise.
        /// </summary>
        Automatic,

        /// <summary>
        /// Threadlink's own drawer, regardless of whether Odin is installed.
        /// </summary>
        Native,

        /// <summary>
        /// Odin's drawer. Falls back to the native drawer when Odin is absent.
        /// </summary>
        Odin
    }

    /// <summary>
    /// Selects which inspector drawer renders a <see cref="Threadlink.Collections.ThreadlinkHashMap{TKey, TValue}"/> field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HashMapDrawerAttribute : PropertyAttribute
    {
        public readonly HashMapDrawerMode Mode;

        public HashMapDrawerAttribute(HashMapDrawerMode mode = HashMapDrawerMode.Automatic) => Mode = mode;
    }
}
