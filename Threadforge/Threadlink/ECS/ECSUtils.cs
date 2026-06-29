namespace Threadlink.Utilities.ECS
{
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;

    public static class ECSUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(in this Entity target)
        {
            return ECSWorld.TryGetSingleton(out var world) && world.IsValid(target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(in this Entity target, out ECSWorld ecsWorld)
        {
            return ECSWorld.TryGetSingleton(out ecsWorld) && ecsWorld.IsValid(target);
        }
    }
}
