namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem.Layouts;

    [Serializable]
    public sealed class DextraInputControlPath
    {
        public static implicit operator string(DextraInputControlPath path) => path?.value;
        public static bool operator ==(DextraInputControlPath a, string b) => a?.value == b;
        public static bool operator !=(DextraInputControlPath a, string b) => !(a == b);
        public static bool operator ==(string a, DextraInputControlPath b) => a == b?.value;
        public static bool operator !=(string a, DextraInputControlPath b) => !(a == b);

        public override bool Equals(object obj) => obj is DextraInputControlPath other && value.Equals(other.value);
        public override string ToString() => value;
        public override int GetHashCode() => value.GetHashCode();

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HideLabel]
#endif
        [SerializeField, InputControl] private string value = null;
    }
}
