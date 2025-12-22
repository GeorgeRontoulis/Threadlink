namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class DextraButton : DextraSelectable<Button>
    {
        protected internal UnityEvent OnClick => selectable.onClick;

        public override void Discard()
        {
            selectable.onClick.RemoveAllListeners();
            selectable.onClick = null;
            base.Discard();
        }
    }
}