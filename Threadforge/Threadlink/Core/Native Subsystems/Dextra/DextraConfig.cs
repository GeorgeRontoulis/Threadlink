namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Collections;
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Dextra Config")]
    public sealed class DextraConfig : ScriptableObject
    {
        public bool HideEventSystemInHierarchy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => hideEventSystemInHierarchy;
        }

        [Serializable] private sealed class InputSpritesMap : FieldHashMap<Dextra.InputDevice, ThreadlinkIDs.Addressables.Assets> { }

        [SerializeField] private ThreadlinkIDs.Addressables.Prefabs[] interfacePointers = Array.Empty<ThreadlinkIDs.Addressables.Prefabs>();

        [Space(10)]

        [SerializeField] private FieldHashMap<ThreadlinkIDs.Dextra.InputModes, InputActionReference> inputMaps = new();

        [Space(10)]

        [SerializeField] private FieldHashMap<DextraInputControlPath, InputSpritesMap> inputIcons = new();

        [Space(10)]

        [SerializeField] private bool hideEventSystemInHierarchy = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetInputMap(ThreadlinkIDs.Dextra.InputModes inputMode, out InputActionMap result)
        {
            if (inputMaps.TryGetValue(inputMode, out var reference) && reference != null && reference.action != null)
            {
                result = reference.action.actionMap;
                return true;
            }

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetInterfacePointers(out ReadOnlySpan<ThreadlinkIDs.Addressables.Prefabs> result) => !(result = interfacePointers).IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputIcon(Dextra.InputDevice device, DextraInputControlPath inputControlPath, out Sprite result)
        {
            if (Threadlink.TryGetSingleton(out var core) && inputIcons.TryGetValue(inputControlPath, out var deviceIconMap))
            {
                if (deviceIconMap.TryGetValue(device, out var iconPointer)
                && core.TryGetAssetReference(iconPointer, out var runtimeKey))
                {
                    result = Threadlink.LoadAsset<Sprite>(runtimeKey);
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
            if (interfacePointers != null && Threadlink.TryGetSingleton(out var core))
            {
                int length = interfacePointers.Length;

                for (int i = 0; i < length; i++)
                    core.ReleasePrefab(interfacePointers[i]);
            }
        }

        internal async UniTask LoadAllUserInterfacesAsync()
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return;

            int length = interfacePointers.Length;
            var tasks = new UniTask[length];

            for (int i = 0; i < length; i++)
                tasks[i] = core.LoadPrefabAsync<UserInterface>(interfacePointers[i]);

            await UniTask.WhenAll(tasks);
        }
    }
}
