namespace Threadlink.Netcode
{
    using System;
    using Threadlink.Shared;

    public interface IFlowProvider : IBootable, IDiscardable, IDisposable
    {
        void HostLobby();
        void JoinLobby(ulong lobbyID);
        void AutoJoinHostLobby();
    }
}
