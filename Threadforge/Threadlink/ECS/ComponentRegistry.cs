namespace Threadlink.ECS
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public static class ComponentRegistry
    {
        public static int ComponentCount { get; private set; }

        internal static void Hydrate()
        {
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsValueType && typeof(IComponent).IsAssignableFrom(type))
                .OrderBy(t => ComponentTypeHash(t))
                .ToArray();

            ComponentCount = componentTypes.Length;

            for (int i = 0; i < ComponentCount; i++)
            {
                var bitIndexField = MakeGenericType(componentTypes[i]).GetField("BitIndex", BindingFlags.Public | BindingFlags.Static);
                bitIndexField?.SetValue(null, i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComponentTypeHash(Type type)
        {
            var field = MakeGenericType(type).GetField("Hash", BindingFlags.Public | BindingFlags.Static);
            return (int)(field?.GetValue(null) ?? 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Type MakeGenericType(Type type)
        {
            return typeof(ComponentType.Of<>).MakeGenericType(type);
        }
    }
}