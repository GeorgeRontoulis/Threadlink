namespace Threadlink.ECS
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Marks a value type implementing <see cref="IComponent"/> for inclusion
    /// in runtime discovery and BitIndex assignment by <see cref="ComponentRegistry"/>.
    /// Only types carrying this attribute are registered — editor, test, and
    /// transient component types should omit it.
    /// </summary>
    /// <remarks>
    /// If your project strips managed code via IL2CPP or a linker, also apply
    /// [UnityEngine.Scripting.Preserve] (or your platform's equivalent) to
    /// prevent the type from being removed before <see cref="ComponentRegistry.Hydrate"/>
    /// runs. The registry will warn you at hydration time if this is missing.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class RuntimeComponentAttribute : Attribute { }

    public static class ComponentRegistry
    {
        public static int ComponentCount { get; private set; }

        // Lazily resolved so the assembly reference stays optional at compile time.
        private static readonly Type PreserveAttributeType = Type.GetType("UnityEngine.Scripting.PreserveAttribute, UnityEngine.CoreModule", throwOnError: false);

        internal static void Hydrate()
        {
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(IsRuntimeComponent)
                .OrderBy(ComponentTypeHash)
                .ToArray();

            if (componentTypes.Length == 0)
            {
                UnityEngine.Debug.LogError("[ComponentRegistry] Hydrate found zero runtime components. Ensure [RuntimeComponent] is applied and Hydrate() runs before any component access.");
                return;
            }

            WarnOnMissingPreserve(componentTypes);
            ComponentCount = componentTypes.Length;

            string msg = "[ComponentRegistry] ";
            for (int i = 0; i < ComponentCount; i++)
            {
                var type = componentTypes[i];
                var ofType = MakeGenericType(type);
                var bitField = ofType.GetField("BitIndex", BindingFlags.Public | BindingFlags.Static);

                if (bitField == null)
                {
                    UnityEngine.Debug.LogError($"[ComponentRegistry] 'BitIndex' field not found on '{ofType.FullName}'. Check that ComponentType.Of<T>.BitIndex is public static and not a property.");
                    continue;
                }

                bitField.SetValue(null, i);

                var written = (int)(bitField.GetValue(null) ?? -1);
                if (written != i)
                {
                    UnityEngine.Debug.LogError($"[ComponentRegistry] SetValue failed for '{type.Name}': expected BitIndex={i}, read back {written}. The field may be readonly or stripped.");
                    continue;
                }

                msg += $"{type.Name} = Bit {i}";
                msg += Environment.NewLine;
            }

            msg += $"[ComponentRegistry] Registered {ComponentCount} component(s).";
            UnityEngine.Debug.Log(msg);
        }

        #region Helper Methods:

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRuntimeComponent(Type type)
        {
            return type.IsValueType && typeof(IComponent).IsAssignableFrom(type) && type.IsDefined(typeof(RuntimeComponentAttribute), inherit: false);
        }

        private static void WarnOnMissingPreserve(Type[] types)
        {
            if (PreserveAttributeType == null) return;

            foreach (var type in types)
            {
                if (!type.IsDefined(PreserveAttributeType, inherit: false))
                    UnityEngine.Debug.LogWarning($"[ComponentRegistry] '{type.FullName}' has [RuntimeComponent] but is missing [Preserve]. It may be stripped by the linker.");
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

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray();
            }
        }
        #endregion
    }
}