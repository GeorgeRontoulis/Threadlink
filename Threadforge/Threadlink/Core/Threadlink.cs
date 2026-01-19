namespace Threadlink.Core
{
    using Cysharp.Threading.Tasks;
    using MessagePack;
    using NativeSubsystems.Iris;
    using NativeSubsystems.Scribe;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// The Core. Controls all aspects of the runtime, managing subsystems at the lowest level.
    /// </summary>
    public sealed partial class Threadlink : Weaver<Threadlink, IThreadlinkSubsystem>
    {
        /// <summary>
        /// <see cref="Native"/> = Instantiate the native <see cref="ThreadlinkLoop"/> 
        /// <see cref="GameObject"/> to start receiving update callbacks through <see cref="Iris"/>.
        /// <para></para>
        /// <see cref="Custom"/> = Use your own logic to instantiate a custom update loop 
        /// <see cref="MonoBehaviour"/> that will publish <see cref="Iris"/>' update events.
        /// Subscribe to <see cref="Iris.Events.OnCoreDeployed"/> to get a callback when
        /// the core is deployed, then set up your update loop there.
        /// <para></para>
        /// This is useful when using Threadlink alongside another framework.
        /// For example, in the context of <see href="https://doc.photonengine.com/quantum/current/quantum-intro">Photon Quantum</see>,
        /// Threadlink would only manage the View, while Quantum would manage the deterministic multiplayer Simulation.
        /// </summary>
        internal enum UpdateLoop : byte { Native, Custom }

        /// <summary>
        /// Uses <see cref="MessagePackSerializerOptions.Standard"/> by default.
        /// You may customize this as you see fit. Use <see cref="Threadlink"/>'s
        /// serialization API to make sure serialization uses these options.
        /// <para></para>
        /// When using <see cref="MessagePack"/> manually, make sure you pass these
        /// or any options from your custom source into all serialization methods.
        /// </summary>
        public static MessagePackSerializerOptions serializerOptions = MessagePackSerializerOptions.Standard;

        internal ThreadlinkNativeConfig NativeConfig { get; set; }
        public ThreadlinkUserConfig UserConfig { get; internal set; }

        #region Main Lifecycle API:
        /// <summary>
        /// Terminate the runtime.
        /// Same as <see cref="Discard"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Terminate() => Instance.Discard();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        public override void Boot()
        {
            base.Boot();

            if (UserConfig != null && UserConfig.UpdateLoopBehaviour is UpdateLoop.Native)
            {
                ///Start the Threadlink Update Loop.
                Object.DontDestroyOnLoad(new GameObject(nameof(ThreadlinkLoop), typeof(ThreadlinkLoop))
                {
                    hideFlags = HideFlags.HideInHierarchy
                });
            }
        }
        #endregion

        #region Public API:
        /// <summary>
        /// Wait for <paramref name="frameCount"/> frames in an async context.
        /// </summary>
        /// <param name="frameCount">The number of frames to wait for.</param>
        /// <returns>The awaitable task.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask WaitForFramesAsync(int frameCount)
        {
            for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Weave<T>() where T : IThreadlinkSubsystem => Instance.TryWeave(out T wovenSubsystem) ? wovenSubsystem : default;

        /// <summary>
        /// Attempt to serialize data into bytes using <see cref="MessagePack"/> and <see cref="serializerOptions"/>.
        /// Any and all requirements and limitations of the serializer apply to the data you want to serialize.
        /// </summary>
        /// <typeparam name="T">The type of data.</typeparam>
        /// <param name="input">The input data.</param>
        /// <param name="result">The resulting byte data.</param>
        /// <returns>A byte array containing the serialized data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySerialize<T>(T input, out byte[] result)
        {
            if (input == null)
            {
                Scribe.Send<Threadlink>("NULL input data detected! Will not serialize!").ToUnityConsole(DebugType.Error);
                result = null;
                return false;
            }

            result = MessagePackSerializer.Serialize(input, serializerOptions);
            return true;
        }

        /// <summary>
        /// Attempt to deserialize byte data into the desired data type using <see cref="MessagePack"/> and <see cref="serializerOptions"/>.
        /// Any and all requirements and limitations of the serializer apply to the data you want to deserialize.
        /// </summary>
        /// <typeparam name="T">The type of data.</typeparam>
        /// <param name="input">The input byte data.</param>
        /// <param name="result">The resulting data.</param>
        /// <returns>The deserialized data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserialize<T>(byte[] input, out T result)
        {
            if (input == null || input.Length <= 0)
            {
                Scribe.Send<Threadlink>("NULL or empty byte data detected! Will not deserialize!").ToUnityConsole(DebugType.Error);
                result = default;
                return false;
            }

            result = MessagePackSerializer.Deserialize<T>(input, serializerOptions);
            return true;
        }
        #endregion
    }
}