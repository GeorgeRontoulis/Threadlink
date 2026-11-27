// Threadlink 1.0 example: engineering settings.
namespace Threadlink.User.Examples
{
    using Threadforge.Threadlink;
    using UnityEngine;

    /// <summary>Engineering settings: edited only in Project Settings ▸ Threadlink ▸ Economy, read-only in the Inspector.</summary>
    public sealed class EconomySettings : ThreadlinkSettings<EconomySettings>
    {
        [Min(0)]
        [Tooltip("Coins a new game starts with.")]
        public int StartingCoins = 10;

        public override string Validate() => StartingCoins > 1000 ? "Starting coins look too high." : null;
    }
}
