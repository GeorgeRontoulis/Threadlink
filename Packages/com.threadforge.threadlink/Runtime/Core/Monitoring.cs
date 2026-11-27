namespace Threadforge.Threadlink
{
    using System.Collections.Generic;
    using System.Globalization;
    using UnityObject = UnityEngine.Object;

    /// <summary>Something whose state the Threadlink Runtime monitor can show. Every subsystem is monitorable.</summary>
    public interface IMonitorable
    {
        /// <summary>
        /// Describes the current state. Always set a <see cref="MonitorReport.Summary"/>; add sections, fields and notes
        /// only while <see cref="MonitorReport.Detailed"/> is true, since summaries are requested many times a second.
        /// </summary>
        void Report(MonitorReport report);
    }

    /// <summary>How a monitored value reads at a glance.</summary>
    public enum MonitorTone : byte
    {
        /// <summary>Ordinary text.</summary>
        Normal,

        /// <summary>Secondary text.</summary>
        Muted,

        /// <summary>Healthy.</summary>
        Good,

        /// <summary>Needs attention soon.</summary>
        Caution,

        /// <summary>Failed or over budget.</summary>
        Bad,
    }

    /// <summary>The kind of a <see cref="MonitorLine"/>.</summary>
    public enum MonitorLineKind : byte
    {
        /// <summary>A heading that starts a group of lines.</summary>
        Section,

        /// <summary>A label and value.</summary>
        Field,

        /// <summary>A label and a Unity object the monitor can ping.</summary>
        Reference,

        /// <summary>A label and value with a bar.</summary>
        Meter,

        /// <summary>A sentence.</summary>
        Note,
    }

    /// <summary>One line of a <see cref="MonitorReport"/>.</summary>
    public readonly struct MonitorLine
    {
        /// <summary>The kind of line.</summary>
        public MonitorLineKind Kind { get; }

        /// <summary>The label, section title or note text.</summary>
        public string Label { get; }

        /// <summary>The value, if any.</summary>
        public string Value { get; }

        /// <summary>How the value reads at a glance.</summary>
        public MonitorTone Tone { get; }

        /// <summary>The object a <see cref="MonitorLineKind.Reference"/> line points to.</summary>
        public UnityObject Target { get; }

        /// <summary>How full a <see cref="MonitorLineKind.Meter"/> line is, from 0 to 1.</summary>
        public float Fraction { get; }

        internal MonitorLine(MonitorLineKind kind, string label, string value, MonitorTone tone, UnityObject target, float fraction)
        {
            Kind = kind;
            Label = label;
            Value = value;
            Tone = tone;
            Target = target;
            Fraction = fraction;
        }
    }

    /// <summary>
    /// What a monitorable reports about itself: a one-line summary and, when <see cref="Detailed"/>, sections of fields,
    /// references, meters and notes. The monitor reuses one report per request, so reporting costs nothing unless the
    /// monitor is open.
    /// </summary>
    public sealed class MonitorReport
    {
        private readonly List<MonitorLine> lines = new List<MonitorLine>(32);

        /// <summary>Whether the full report is wanted. When false, set only the summary.</summary>
        public bool Detailed { get; private set; }

        /// <summary>A short status, such as "4 voices" or "Ready".</summary>
        public string Caption { get; private set; }

        /// <summary>Whether something needs attention.</summary>
        public bool Alert { get; private set; }

        /// <summary>The detailed lines.</summary>
        public IReadOnlyList<MonitorLine> Lines => lines;

        /// <summary>Clears the report for a new request.</summary>
        public void Begin(bool detailed)
        {
            Detailed = detailed;
            Caption = null;
            Alert = false;
            lines.Clear();
        }

        /// <summary>Sets the short status, and whether something needs attention.</summary>
        public MonitorReport Summary(string caption, bool alert = false)
        {
            Caption = caption;
            Alert = alert;
            return this;
        }

        /// <summary>Starts a section.</summary>
        public MonitorReport Section(string title) => Add(MonitorLineKind.Section, title, null, MonitorTone.Normal, null, 0f);

        /// <summary>Adds a field.</summary>
        public MonitorReport Field(string label, string value, MonitorTone tone = MonitorTone.Normal) =>
            Add(MonitorLineKind.Field, label, value, tone, null, 0f);

        /// <summary>Adds a numeric field.</summary>
        public MonitorReport Field(string label, long value, MonitorTone tone = MonitorTone.Normal) =>
            Detailed ? Field(label, value.ToString(CultureInfo.InvariantCulture), tone) : this;

        /// <summary>Adds a numeric field.</summary>
        public MonitorReport Field(string label, double value, MonitorTone tone = MonitorTone.Normal, string format = "0.##") =>
            Detailed ? Field(label, value.ToString(format, CultureInfo.InvariantCulture), tone) : this;

        /// <summary>Adds a yes/no field.</summary>
        public MonitorReport Field(string label, bool value, MonitorTone tone = MonitorTone.Normal) =>
            Field(label, value ? "Yes" : "No", tone);

        /// <summary>A field whose value pings <paramref name="target"/> when clicked. The value defaults to its name.</summary>
        public MonitorReport Reference(string label, UnityObject target, string value = null) =>
            Detailed
                ? Add(MonitorLineKind.Reference, label, value ?? (target != null ? target.name : "None"), MonitorTone.Normal, target, 0f)
                : this;

        /// <summary>A field with a bar, <paramref name="fraction"/> full.</summary>
        public MonitorReport Meter(string label, float fraction, string value, MonitorTone tone = MonitorTone.Normal) =>
            Add(MonitorLineKind.Meter, label, value, tone, null, fraction < 0f ? 0f : fraction > 1f ? 1f : fraction);

        /// <summary>Adds a sentence.</summary>
        public MonitorReport Note(string text, MonitorTone tone = MonitorTone.Muted) =>
            Add(MonitorLineKind.Note, text, null, tone, null, 0f);

        private MonitorReport Add(MonitorLineKind kind, string label, string value, MonitorTone tone, UnityObject target, float fraction)
        {
            if (Detailed)
                lines.Add(new MonitorLine(kind, label, value, tone, target, fraction));
            return this;
        }
    }
}
