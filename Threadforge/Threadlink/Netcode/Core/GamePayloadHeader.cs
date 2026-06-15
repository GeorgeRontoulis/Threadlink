namespace Threadlink.Netcode
{
    public enum GamePayloadHeader : byte
    {
        None = 0,
        ConnectionHandshake,
        PositionUpdate,
        AnimatorUpdate,
        EntitySpawnAction,
    }
}
