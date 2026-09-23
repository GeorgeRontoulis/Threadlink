namespace Threadlink.Core
{
    using Cysharp.Threading.Tasks;
    using MessagePack;
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The Core. Controls all aspects of the runtime, managing subsystems at the lowest level.
    /// </summary>
    public sealed partial class Threadlink : Weaver<Threadlink, IThreadlinkSubsystem>
    {
        /// <summary>
        /// <see cref="Native"/> = Inject Threadlink's update dispatch points
        /// directly into Unity's PlayerLoop.
        /// <para></para>
        /// <see cref="Custom"/> = Threadlink does not install update callbacks.
        /// The project is responsible for publishing Iris' OnUpdate,
        /// OnFixedUpdate and OnLateUpdate events.
        /// <para></para>
        /// This is useful when using Threadlink alongside another framework.
        /// For example, in the context of <see href="https://doc.photonengine.com/quantum/current/quantum-intro">Photon Quantum</see>,
        /// Threadlink would only manage the View, while Quantum would manage the deterministic multiplayer Simulation.
        /// </summary>
        internal enum UpdateLoop : byte { Native, Custom }

        public ThreadlinkNativeConfig NativeConfig { get; set; }
        public ThreadlinkUserConfig UserConfig { get; internal set; }

        #region Main Lifecycle API:
        /// <summary>
        /// Terminate the runtime.
        /// Same as <see cref="Discard"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Terminate()
        {
            if (TryGetSingleton(out var core))
                core.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            ThreadlinkPlayerLoop.Uninstall();

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
                ThreadlinkPlayerLoop.Install();
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
        public static T Weave<T>() where T : IThreadlinkSubsystem
        {
            return TryGetSingleton(out var core) && core.TryWeave(out T wovenSubsystem) ? wovenSubsystem : default;
        }

        /// <summary>
        /// Attempt to serialize data into bytes using <see cref="MessagePack"/> and <see cref="serializerOptions"/>.
        /// Any and all requirements and limitations of the serializer apply to the data you want to serialize.
        /// </summary>
        /// <typeparam name="T">The type of data.</typeparam>
        /// <param name="input">The input data.</param>
        /// <param name="result">The resulting byte data.</param>
        /// <returns>A byte array containing the serialized data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySerialize<T>(T input, out byte[] result, MessagePackSerializerOptions options = null)
        {
            if (input == null)
            {
                Scribe.Send<Threadlink>("NULL input data detected! Will not serialize!").ToUnityConsole(DebugType.Error);
                result = null;
                return false;
            }

            result = MessagePackSerializer.Serialize(input, options ?? MessagePackSerializerOptions.Standard);
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
        public static bool TryDeserialize<T>(byte[] input, out T result, MessagePackSerializerOptions options = null)
        {
            if (input == null || input.Length <= 0)
            {
                Scribe.Send<Threadlink>("NULL or empty byte data detected! Will not deserialize!").ToUnityConsole(DebugType.Error);
                result = default;
                return false;
            }

            result = MessagePackSerializer.Deserialize<T>(input, options ?? MessagePackSerializerOptions.Standard);
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
        public static bool TryDeserialize<T>(ReadOnlyMemory<byte> input, out T result, MessagePackSerializerOptions options = null)
        {
            if (input.IsEmpty)
            {
                Scribe.Send<Threadlink>("Empty byte data detected! Will not deserialize!").ToUnityConsole(DebugType.Error);
                result = default;
                return false;
            }

            result = MessagePackSerializer.Deserialize<T>(input, options ?? MessagePackSerializerOptions.Standard);
            return true;
        }
        #endregion
    }
}