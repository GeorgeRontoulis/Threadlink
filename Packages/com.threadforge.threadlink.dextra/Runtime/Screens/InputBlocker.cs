namespace Threadforge.Threadlink
{
    using System;

    /// <summary>
    /// Blocks gameplay input while a screen is open by claiming the interface map of player 0 above gameplay. Resolves the
    /// player when a screen opens, so players added later are covered. Without a player or an interface map, screens
    /// open without blocking input.
    /// </summary>
    internal sealed class InputBlocker : IInputBlocker
    {
        private sealed class Nothing : IDisposable
        {
            internal static readonly Nothing Instance = new Nothing();

            public void Dispose() { }
        }

        private sealed class Counted : IDisposable
        {
            private readonly InputBlocker owner;
            private IDisposable claim;

            internal Counted(InputBlocker owner, IDisposable claim)
            {
                this.owner = owner;
                this.claim = claim;
            }

            public void Dispose()
            {
                var held = claim;
                if (held == null)
                    return;
                claim = null;
                held.Dispose();
                if (--owner.active == 0)
                    owner.BlockChanged?.Invoke(false);
            }
        }

        private readonly Func<PlayerInputContext> player;
        private readonly Func<string> map;
        private int active;

        /// <summary>Called with true when the first blocking screen opens and false when the last one closes.</summary>
        internal Action<bool> BlockChanged;

        public InputBlocker(Func<PlayerInputContext> player, Func<string> map)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public IDisposable Claim(ScreenLayer layer)
        {
            var target = player();
            string name = target != null ? DextraSettings.Current.MapName(target.Actions, map()) : null;
            if (string.IsNullOrEmpty(name))
                return Nothing.Instance;
            var claim = new Counted(this, target.ClaimByName(100 + (int)layer, true, name));
            if (active++ == 0)
                BlockChanged?.Invoke(true);
            return claim;
        }
    }
}
