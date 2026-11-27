namespace Threadforge.Threadlink
{
    using System;

    /// <summary>
    /// Announced on <see cref="Iris"/> once the session has started: every registered subsystem has booted and initialized,
    /// or failed. Subscribe from <see cref="ThreadlinkSubsystem.OnSubsystemBoot"/>.
    /// </summary>
    public readonly struct ThreadlinkReady
    {
        /// <summary>The session that started.</summary>
        public readonly Guid Session;

        /// <summary>Subsystems that started.</summary>
        public readonly int Ready;

        /// <summary>Subsystems that failed to boot or initialize; each was announced with <see cref="SubsystemFailed"/>.</summary>
        public readonly int Failed;

        /// <summary>Creates the event.</summary>
        public ThreadlinkReady(Guid session, int ready, int failed)
        {
            Session = session;
            Ready = ready;
            Failed = failed;
        }
    }

    /// <summary>Announced on <see cref="Iris"/> when a subsystem fails to boot or initialize, for crash reporting and diagnostics.</summary>
    public readonly struct SubsystemFailed
    {
        /// <summary>The subsystem's class.</summary>
        public readonly Type Subsystem;

        /// <summary>The subsystem's display name.</summary>
        public readonly string Name;

        /// <summary>Why it failed.</summary>
        public readonly Exception Error;

        /// <summary>Creates the event.</summary>
        public SubsystemFailed(Type subsystem, string name, Exception error)
        {
            Subsystem = subsystem;
            Name = name;
            Error = error;
        }
    }

    /// <summary>
    /// Announced on <see cref="Iris"/> when the session starts to shut down, while every subsystem is still available: the
    /// last moment to save. Handlers run synchronously; work that has to wait must already be done.
    /// </summary>
    public readonly struct SessionEnding
    {
        /// <summary>The session that is ending.</summary>
        public readonly Guid Session;

        /// <summary>Creates the event.</summary>
        public SessionEnding(Guid session) => Session = session;
    }
}
