namespace Threadlink.Netcode
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using UnityEngine;

    public sealed class NetworkClipLibrary : ThreadlinkSubsystem<NetworkClipLibrary>
    {
        private Dictionary<int, AnimationClip> ClipRegistry { get; set; }

        public override void Discard()
        {
            if (ClipRegistry != null)
            {
                ClipRegistry.Clear();
                ClipRegistry = null;
            }

            base.Discard();
        }

        public override void Boot()
        {
            ClipRegistry ??= new(64);
            base.Boot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterClip(int hash, AnimationClip clip)
        {
            if (ClipRegistry.TryGetValue(hash, out var existingClip))
            {
                if (existingClip != clip)
                {
                    Scribe.Send<Netrunner>(
                    $"Hash collision detected for clip '{clip.name}'. Animation clip names must be globally unique across the project.",
                    DebugType.Error).ToUnityConsole();
                }
                return;
            }

            ClipRegistry[hash] = clip;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetClip(int hash, out AnimationClip clip)
        {
            return ClipRegistry.TryGetValue(hash, out clip);
        }
    }
}