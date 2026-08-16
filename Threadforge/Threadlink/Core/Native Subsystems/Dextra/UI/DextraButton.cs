namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class DextraButton : DextraSelectable<Button>
    {
        public UnityEvent OnClick
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => selectable.onClick;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            selectable.onClick.RemoveAllListeners();
            selectable.onClick = null;
            base.Discard();
        }
    }
}