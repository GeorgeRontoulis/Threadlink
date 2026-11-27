namespace Threadforge.Threadlink
{
    using System;

    /// <summary>
    /// Announced on Iris when a platform provider is added, such as Steam once it has started: platform features are now
    /// routed to it.
    /// </summary>
    public readonly struct PlatformReady
    {
        /// <summary>The provider's identity, such as "steam".</summary>
        public readonly string Provider;

        /// <summary>The capabilities now routed to it.</summary>
        public readonly Capability Capabilities;

        /// <summary>Creates the event.</summary>
        public PlatformReady(string provider, Capability capabilities)
        {
            Provider = provider;
            Capabilities = capabilities;
        }
    }

    /// <summary>
    /// Announced on Iris when the signed-in account changes, and once for the startup account during the initialization
    /// phase. Saves switch to the new account.
    /// </summary>
    public readonly struct AccountChanged
    {
        /// <summary>The previous account, or null.</summary>
        public readonly AccountIdentity? Previous;

        /// <summary>The account now signed in, or null.</summary>
        public readonly AccountIdentity? Current;

        /// <summary>Creates the event.</summary>
        public AccountChanged(AccountIdentity? previous, AccountIdentity? current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Announced on Iris when a platform captures a join request, such as a Steam friend's invite or a launch command. The
    /// intent also stays queued until taken from <see cref="IJoinIntents"/>, for a game not ready to act on it yet.
    /// </summary>
    public readonly struct JoinIntentReceived
    {
        /// <summary>The join request.</summary>
        public readonly JoinIntent Intent;

        /// <summary>Creates the event.</summary>
        public JoinIntentReceived(JoinIntent intent) => Intent = intent;
    }

    /// <summary>Announced on Iris when an achievement is unlocked through Sentinel: show an in-game toast.</summary>
    public readonly struct AchievementUnlocked
    {
        /// <summary>The achievement.</summary>
        public readonly AchievementId Achievement;

        /// <summary>Creates the event.</summary>
        public AchievementUnlocked(AchievementId achievement) => Achievement = achievement;
    }

    /// <summary>The save operation a <see cref="SaveFailed"/> event reports.</summary>
    public enum SaveOperation : byte
    {
        /// <summary>Writing a slot.</summary>
        Save,

        /// <summary>Reading a slot.</summary>
        Load,

        /// <summary>Deleting a slot.</summary>
        Delete,

        /// <summary>Restoring a slot's previous version.</summary>
        Restore,
    }

    /// <summary>Announced on Iris after a save is written: show an autosave indicator.</summary>
    public readonly struct SaveWritten
    {
        /// <summary>The save data type.</summary>
        public readonly Type Save;

        /// <summary>The slot, numbered from 0.</summary>
        public readonly int Slot;

        /// <summary>The written slot's summary.</summary>
        public readonly SlotSummary Summary;

        /// <summary>Creates the event.</summary>
        public SaveWritten(Type save, int slot, SlotSummary summary)
        {
            Save = save;
            Slot = slot;
            Summary = summary;
        }
    }

    /// <summary>
    /// Announced on Iris when a save cannot be written, read, deleted or restored. An empty slot (NotFound) and a cancelled
    /// operation are not failures.
    /// </summary>
    public readonly struct SaveFailed
    {
        /// <summary>The save data type.</summary>
        public readonly Type Save;

        /// <summary>The slot, numbered from 0.</summary>
        public readonly int Slot;

        /// <summary>What was attempted.</summary>
        public readonly SaveOperation Operation;

        /// <summary>Why it failed.</summary>
        public readonly PersistenceError Error;

        /// <summary>A description of the failure, when the store gave one.</summary>
        public readonly string Message;

        /// <summary>Creates the event.</summary>
        public SaveFailed(Type save, int slot, SaveOperation operation, PersistenceError error, string message)
        {
            Save = save;
            Slot = slot;
            Operation = operation;
            Error = error;
            Message = message;
        }
    }

    /// <summary>Announced on Iris after a slot is restored to its previous version, such as after corrupt data.</summary>
    public readonly struct SaveRecovered
    {
        /// <summary>The save data type.</summary>
        public readonly Type Save;

        /// <summary>The slot, numbered from 0.</summary>
        public readonly int Slot;

        /// <summary>Creates the event.</summary>
        public SaveRecovered(Type save, int slot)
        {
            Save = save;
            Slot = slot;
        }
    }
}
