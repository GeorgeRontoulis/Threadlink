namespace Threadlink.ECS
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A zero-allocation, statically-composed query filter for <see cref="ECSWorld"/> iteration.
    /// Build with the fluent <see cref="With{T}"/> and <see cref="Without{T}"/> methods, then
    /// pass directly to any <c>ECSWorld.ForEach</c> overload.
    /// </summary>
    public readonly struct ECSFilter
    {
        public readonly ComponentMask Include;
        public readonly ComponentMask Exclude;

        private ECSFilter(ComponentMask include, ComponentMask exclude)
        {
            Include = include;
            Exclude = exclude;
        }

        /// <summary>Returns a new filter requiring component <typeparamref name="T"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ECSFilter With<T>() where T : unmanaged, IComponent
        {
            var include = Include;
            include.Set(ComponentType.Of<T>.BitIndex);
            return new ECSFilter(include, Exclude);
        }

        /// <summary>Returns a new filter that rejects entities carrying component <typeparamref name="T"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ECSFilter Without<T>() where T : unmanaged, IComponent
        {
            var exclude = Exclude;
            exclude.Set(ComponentType.Of<T>.BitIndex);
            return new ECSFilter(Include, exclude);
        }

        /// <summary>
        /// True when <paramref name="entityMask"/> contains every included bit
        /// and none of the excluded bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Matches(in ComponentMask entityMask)
        {
            return entityMask.Matches(Include) && !entityMask.HasAnyFrom(Exclude);
        }
    }
}
