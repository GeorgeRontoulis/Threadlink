namespace Threadforge.Threadlink
{
    using UnityEngine;

    /// <summary>Non-generic base of <see cref="ThreadlinkSettings{T}"/>, for tooling.</summary>
    public abstract class ThreadlinkSettings : ScriptableObject
    {
        /// <summary>Checks the settings, returning a problem description or null. The Workspace validator shows problems.</summary>
        public virtual string Validate() => null;

        /// <summary>The Project Settings page name, such as "Dextra".</summary>
        public virtual string PageName => GetType().Name.EndsWith("Settings") ? GetType().Name.Substring(0, GetType().Name.Length - 8) : GetType().Name;
    }

    /// <summary>
    /// Engineering settings for one Threadlink package, edited in <b>Project Settings ▸ Threadlink</b>. The asset lives in
    /// the project's Engineering/Configs folder, is read-only in the Inspector, and ships with players as a preloaded
    /// asset. Subsystems read <see cref="Current"/>; without an asset they use the defaults.
    /// </summary>
    /// <typeparam name="T">The deriving settings class.</typeparam>
    public abstract class ThreadlinkSettings<T> : ThreadlinkSettings
        where T : ThreadlinkSettings<T>
    {
        private static T loaded;
        private static T defaults;

        /// <summary>Key under which the Editor registers the settings asset.</summary>
        public static string ConfigKey => "com.threadforge.threadlink.settings." + typeof(T).Name;

        /// <summary>The project's settings, or defaults when the project has none.</summary>
        public static T Current
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorBuildSettings.TryGetConfigObject<T>(ConfigKey, out var configured) && configured != null)
                    return configured;
#endif
                if (loaded != null)
                    return loaded;
                if (defaults == null)
                {
                    defaults = CreateInstance<T>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        /// <summary>Registers the preloaded asset in players.</summary>
        protected virtual void OnEnable()
        {
#if !UNITY_EDITOR
            loaded = (T)this;
#endif
        }
    }
}
