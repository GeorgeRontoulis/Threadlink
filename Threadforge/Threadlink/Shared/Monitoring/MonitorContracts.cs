namespace Threadlink.Shared
{
    using System;

    /// <summary>
    /// Something whose state can be monitored, such as in the Runtime Monitor.
    /// <para></para>
    /// Every <c>ThreadlinkSubsystem</c> is monitorable: override its <c>Report</c> to describe its state. Other objects
    /// implement this interface and register with <see cref="MonitorRegistry"/>. Static services mark a static
    /// <c>Report(MonitorReport)</c> method with <see cref="MonitoredAttribute"/> instead.
    /// </summary>
    public interface IMonitorable
    {
        /// <summary>
        /// Describe the current state. Always set a <see cref="MonitorReport.Summary"/>; add sections, fields and notes
        /// only while <see cref="MonitorReport.Detailed"/> is true, since summaries are requested many times a second.
        /// </summary>
        public void Report(MonitorReport report);
    }

    /// <summary>How a monitored value reads at a glance.</summary>
    public enum MonitorTone : byte { Normal, Muted, Good, Caution, Bad }

    /// <summary>
    /// Declares a monitored service, or describes a monitored subsystem.
    /// <list type="bullet">
    /// <item>On a <c>static void Report(MonitorReport report)</c> method: the declaring class is a static service the
    /// Runtime Monitor shows as a node, named <see cref="Name"/> or after the class.</item>
    /// <item>On a subsystem class: its description and order, shown before it is woven.</item>
    /// </list>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MonitoredAttribute : Attribute
    {
        public MonitoredAttribute() { }

        public MonitoredAttribute(string name) => Name = name;

        /// <summary>The name shown for the node. Defaults to the class name.</summary>
        public string Name { get; }

        /// <summary>What the service or subsystem does, in a sentence. Shown when it is not running.</summary>
        public string Description { get; set; }

        /// <summary>Nodes sort by order, then name. Threadlink's own use negative values.</summary>
        public int Order { get; set; }
    }
}
