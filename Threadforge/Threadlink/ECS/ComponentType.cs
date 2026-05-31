namespace Threadlink.ECS
{
    using System;
    using Threadlink.Shared;

    public interface IComponent : IDisposable { }

    public static class ComponentType
    {
        public static class Of<T> where T : unmanaged, IComponent
        {
            public static readonly int Hash = HashFunctions.ToXxHash32(typeof(T).AssemblyQualifiedName ?? typeof(T).FullName);
            public static int BitIndex = -1; // Assigned deterministically by ComponentRegistry at boot
        }
    }
}