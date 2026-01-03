namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Sentinel Config")]
    public sealed class SentinelConfig : ScriptableObject
    {
        internal Sentinel.Environment TargetEnvironment => targetEnvironment;

#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#endif
        [SerializeReference]
        private Sentinel.Environment targetEnvironment = null;
    }
}
