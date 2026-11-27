namespace Threadforge.Threadlink.Editor
{
    using UnityEditor.Scripting.LifecycleManagement;

    /// <summary>Ends the Threadlink session when the Editor returns to Edit mode, after the scene has been torn down.</summary>
    internal static partial class SessionLifecycle
    {
        [OnEnteringEditMode]
        private static void OnEnteringEditMode() => Threadlink.ShutdownCurrent();
    }
}
