namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public abstract class InteractableUserInterface<Singleton, Selectable> : UserInterface<Singleton>, IInteractableInterface<Selectable>
    where Singleton : InteractableUserInterface<Singleton, Selectable>
    where Selectable : DextraSelectable
    {
        public abstract List<Selectable> Selectables { get; }
        public Selectable LastSelectable { get; private set; }

        public override void Discard()
        {
            var selectables = Selectables;

            if (selectables != null)
            {
                int length = selectables.Count;

                for (int i = 0; i < length; i++)
                    selectables[i]?.Discard();
            }

            selectables.Clear();
            selectables.TrimExcess();
            LastSelectable = null;
            base.Discard();
        }

        public override void Boot()
        {
            base.Boot();

            var selectables = Selectables;

            if (selectables != null)
            {
                int count = selectables.Count;

                for (int i = 0; i < count; i++)
                    selectables[i].OnSelected += UpdateLastSelectable;
            }

            if (selectables != null && selectables.Count > 0)
                LastSelectable = selectables[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void UpdateLastSelectable(DextraSelectable newSelection) => LastSelectable = newSelection as Selectable;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void SelectLastSelectable()
        {
            if (LastSelectable != null)
                Dextra.Instance.SelectUIElement(LastSelectable.gameObject).Forget();
        }

        protected internal override void OnStacked()
        {
            base.OnStacked();
            SelectLastSelectable();
        }

        protected internal override void OnResurfaced()
        {
            base.OnResurfaced();
            SelectLastSelectable();
        }
    }
}
