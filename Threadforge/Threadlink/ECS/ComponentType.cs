namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;

    public interface IComponent : IDisposable { }

    public static class ComponentType
    {
        public static class Of<T> where T : unmanaged, IComponent
        {
            public static readonly int Bit = Next();
        }

        private static int bit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Next() => bit++;
    }
}