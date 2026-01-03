namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Addressables;
    using Collections;
    using Collections.Extensions;
    using Cysharp.Threading.Tasks;
    using Scribe;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [CreateAssetMenu(menuName = "Threadlink/Subsystem Dependencies/Dextra Config")]
    public sealed class DextraConfig : ScriptableObject
    {
        [Serializable]
        private sealed class NestedFieldTable : FieldTable<Dextra.InputDevice, GroupedAssetPointer> { }

        [SerializeField] private GroupedAssetPointer[] interfacePointers = new GroupedAssetPointer[0];

        [Space(10)]

        [SerializeField] private FieldTable<DextraInputControlPath, NestedFieldTable> inputIcons = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetInterfacePointers(out ReadOnlySpan<GroupedAssetPointer> result) => !(result = interfacePointers).IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputIcon(Dextra.InputDevice device, DextraInputControlPath inputControlPath, out Sprite result)
        {
            if (inputIcons.TryGetValue(inputControlPath, out var deviceIconMap))
            {
                if (deviceIconMap.TryGetValue(device, out var iconPointer)
                && Threadlink.TryGetAssetReference(iconPointer.Group, iconPointer.IndexInDatabase, out var runtimeKey))
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
    }
}
