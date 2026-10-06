#if UNITY_EDITOR
namespace Threadlink.Core
{
    /// <summary>Editor-only deployment control. Set before AfterSceneLoad to preview a scene without the core.</summary>
    public static partial class ThreadlinkEditorDeployment
    {
        /// <summary>Skip Addressables initialization and core deployment for this Play session; resets at Play entry/exit.</summary>
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        public static bool Suppressed { get; set; }
    }
}
#endif
