namespace Threadlink.Core.NativeSubsystems.Dextra
{
#if UNITY_EDITOR
    using InputIconsAuthoringTable = Authoring.ThreadlinkSerializableAuthoringTable
    <
        UnityEngine.InputSystem.InputActionReference,
        Authoring.ThreadlinkAuthoringTable<Dextra.InputDevice, UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.Sprite>>
    >;
#endif
    using InputIconsMap = System.Collections.Generic.Dictionary<System.Guid, System.Collections.Generic.Dictionary<Dextra.InputDevice, string>>;

    using Addressables;
    using Cysharp.Threading.Tasks;
    using Scribe;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Shared;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.InputSystem;
    using Utilities.Strings;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Dextra Config")]
    public sealed class DextraConfig : ScriptableObject, IAsyncBinaryConsumer, IBinaryAuthor
    {
        private InputIconsMap InputIconsMap { get; set; }

        #region Runtime:
        [Header("Runtime Properties:")]
        [Space(10)]

        [SerializeField] private GroupedAssetPointer[] interfacePointers = new GroupedAssetPointer[0];

        [Space(10)]

        [SerializeField] private AssetReferenceT<TextAsset> inputIconMapBinary = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetInterfacePointers(out ReadOnlySpan<GroupedAssetPointer> result) => !(result = interfacePointers).IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputIcon(Dextra.InputDevice device, Guid actionID, out Sprite result)
        {
            if (InputIconsMap.TryGetValue(actionID, out var deviceIconMap))
            {
                if (deviceIconMap.TryGetValue(device, out var runtimeKey) && !string.IsNullOrEmpty(runtimeKey))
                {
                    result = ThreadlinkResourceProvider<Sprite>.LoadOrGetCachedAt(runtimeKey);
                    return true;
                }
                else
                {
                    this.Send("Could not find icon!").ToUnityConsole(DebugType.Error);
                    result = null;
                    return false;
                }
            }
            else
            {
                this.Send("Could not find device/icon map!").ToUnityConsole(DebugType.Error);
                result = null;
                return false;
            }
        }

        internal void UnloadAllUserInterfaces()
        {
            if (interfacePointers != null)
            {
                int length = interfacePointers.Length;

                for (int i = 0; i < length; i++)
                {
                    var pointer = interfacePointers[i];
                    Threadlink.ReleasePrefab(pointer.Group, pointer.IndexInDatabase);
                }
            }
        }

        internal async UniTask LoadAllUserInterfacesAsync()
        {
            int length = interfacePointers.Length;
            var tasks = new UniTask[length];

            for (int i = 0; i < length; i++)
            {
                var pointer = interfacePointers[i];
                tasks[i] = Threadlink.LoadPrefabAsync<UserInterface>(pointer.Group, pointer.IndexInDatabase);
            }

            await UniTask.WhenAll(tasks);
        }

        public async UniTask ConsumeBinariesAsync()
        {
            InputIconsMap = await inputIconMapBinary.DeserializeIntoDictionaryAsync<Guid, Dictionary<Dextra.InputDevice, string>>();
        }
        #endregion

#if UNITY_EDITOR
        #region Edit-time Properties:
        [Header("Edit-time Properties:")]
        [Space(10)]

        [SerializeField] private InputIconsAuthoringTable inputIconsAuthoringTable = new();

        [ContextMenu("Serialize Authoring Data Into Binary")]
        public void SerializeAuthoringDataIntoBinary()
        {
            InputIconsMap ConvertToSerializableRuntimeData(InputIconsAuthoringTable authoringData)
            {
                var result = new InputIconsMap(authoringData.Count);

                foreach (var entry in authoringData)
                {
                    var deviceIconRefs = entry.Value;
                    var runtimeDeviceIconRefs = new Dictionary<Dextra.InputDevice, string>(deviceIconRefs.Count);

                    foreach (var deviceIconRefPair in deviceIconRefs)
                    {
                        var device = deviceIconRefPair.Key;
                        var spriteRef = deviceIconRefPair.Value;

                        if (!runtimeDeviceIconRefs.TryAdd(device, spriteRef.RuntimeKey.ToString()))
                            this.Send($"Duplicate key detected: {device}").ToUnityConsole(DebugType.Warning);
                    }

                    var actionID = entry.Key.action.id;

                    if (!result.TryAdd(actionID, runtimeDeviceIconRefs))
                        this.Send($"Duplicate key detected: {actionID}").ToUnityConsole(DebugType.Warning);
                }

                return result;
            }

            inputIconsAuthoringTable.SerializeIntoBinary(ConvertToSerializableRuntimeData(inputIconsAuthoringTable), "DextraInputIconDatabase");
        }
        #endregion
#endif
    }
}
