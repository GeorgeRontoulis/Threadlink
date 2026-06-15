namespace Threadlink.Netcode
{
    using Core;
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Utilities.Flags;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Netflow Config")]
    public sealed class NetflowConfig : ExternalConfig
    {
        [Flags]
        private enum SessionOptions : byte
        {
            RemoteClient = 0, // Explicitly defining 0 as a valid Remote Client state
            LocalMode = 1 << 0,
            HostMode = 1 << 1,
        }

        public bool LocalMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sessionOptions.HasFlagUnsafe(SessionOptions.LocalMode);
        }

        public bool HostMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sessionOptions.HasFlagUnsafe(SessionOptions.HostMode);
        }

        [SerializeField] private SessionOptions sessionOptions = SessionOptions.LocalMode | SessionOptions.HostMode;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFlowProvider(out IFlowProvider result)
        {
            result = LocalMode ? new LocalSteamFlowProvider() : new RemoteSteamFlowProvider();
            return result != null;
        }
    }
}