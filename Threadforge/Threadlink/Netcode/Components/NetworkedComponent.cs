namespace Threadlink.Netcode
{
    using ECS;

    public interface INetworkedComponent : IComponent
    {
        public uint NetworkTick { get; set; }
    }
}
