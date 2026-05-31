namespace Threadlink.Utilities.Subsystems
{
    using System.Runtime.CompilerServices;
    using Threadlink.Shared;

    public static class ThreadlinkSubsystems
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register<T>() where T : IThreadlinkSubsystem, new()
        {
            WeavingFactory<T>.OnCreate += static () => new T();
        }
    }
}
