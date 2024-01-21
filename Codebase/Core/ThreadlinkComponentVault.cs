namespace Threadlink.Core
{
    using System.Collections.Generic;
    using MassTransit;

    /// <summary>
    /// Threadlink's main Data Container interface.
    /// </summary>
    public interface IThreadlinkComponent { }

    public struct PositionComponent : IThreadlinkComponent
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public struct RotationComponent : IThreadlinkComponent
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public struct ScaleComponent : IThreadlinkComponent
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public struct ThreadlinkComponentVault
    {
        public Dictionary<NewId, PositionComponent> PositionComponents { get; set; }
        public Dictionary<NewId, RotationComponent> RotationComponents { get; set; }
        public Dictionary<NewId, ScaleComponent> ScaleComponents { get; set; }
    }
}
