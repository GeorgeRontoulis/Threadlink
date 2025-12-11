namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Sentinel Config")]
    public sealed class SentinelConfig : ScriptableObject
    {
        internal Sentinel.Environment TargetEnvironment => targetEnvironment;

        [SerializeReference, SerializeReferenceButton]
        private Sentinel.Environment targetEnvironment = null;
    }
}
