namespace Threadlink.Core.NativeSubsystems.Chronos
{
    using System.Runtime.CompilerServices;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Chronos Config")]
    public sealed class ChronosConfig : ScriptableObject
    {
        /// <summary>
        /// Update the Physics Engine through <see cref="Iris.Iris"/>?
        /// </summary>
        internal bool IrisPhysicsUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => irisPhysicsUpdate;
        }

        [Tooltip("Whether to manually simulate Physics through Iris. Set this to false if another framework depends on running the simulation on its own.")]
        [SerializeField] private bool irisPhysicsUpdate = false;
    }
}
