namespace Threadlink.Shared
{
    using System.Collections.Generic;
    using System.Globalization;
    using UnityObject = UnityEngine.Object;

    public enum MonitorLineKind : byte { Section, Field, Reference, Meter, Note }

    /// <summary>One line of a <see cref="MonitorReport"/>.</summary>
    public readonly struct MonitorLine
    {
        public MonitorLineKind Kind { get; }
        public string Label { get; }
        public string Value { get; }
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
    /// What an <see cref="IMonitorable"/> reports about itself: a one-line summary, and, when <see cref="Detailed"/>,
    /// sections of fields, references, meters and notes. A monitor reuses one report, calling <see cref="Begin"/> before
    /// each request. Reporting allocates nothing until a monitor asks, so the cost is paid only while monitoring.
    /// </summary>
    public sealed class MonitorReport
    {
        private readonly List<MonitorLine> lines = new(32);

        /// <summary>Whether the full report is wanted. When false, set only the <see cref="Summary"/>.</summary>
        public bool Detailed { get; private set; }

        /// <summary>A short status, such as "4 linked" or "Ready".</summary>
        public string Caption { get; private set; }

        /// <summary>Whether something needs attention.</summary>
        public bool Alert { get; private set; }

        public IReadOnlyList<MonitorLine> Lines => lines;

        /// <summary>Clears the report for a new request.</summary>
        public void Begin(bool detailed)
        {
            Detailed = detailed;
            Caption = null;
            Alert = false;
            lines.Clear();
        }

        /// <summary>Sets the short status shown on the node, and whether something needs attention.</summary>
        public MonitorReport Summary(string caption, bool alert = false)
        {
            Caption = caption;
            Alert = alert;
            return this;
        }

        public MonitorReport Section(string title) => Add(MonitorLineKind.Section, title, null, MonitorTone.Normal, null, 0f);

        public MonitorReport Field(string label, string value, MonitorTone tone = MonitorTone.Normal)
        {
            return Add(MonitorLineKind.Field, label, value, tone, null, 0f);
        }

        public MonitorReport Field(string label, int value, MonitorTone tone = MonitorTone.Normal)
        {
            return Detailed ? Field(label, value.ToString(CultureInfo.InvariantCulture), tone) : this;
        }

        public MonitorReport Field(string label, long value, MonitorTone tone = MonitorTone.Normal)
        {
            return Detailed ? Field(label, value.ToString(CultureInfo.InvariantCulture), tone) : this;
        }

        public MonitorReport Field(string label, float value, MonitorTone tone = MonitorTone.Normal, string format = "0.##")
        {
            return Detailed ? Field(label, value.ToString(format, CultureInfo.InvariantCulture), tone) : this;
        }

        public MonitorReport Field(string label, bool value, MonitorTone tone = MonitorTone.Normal)
        {
            return Field(label, value ? "Yes" : "No", tone);
        }

        /// <summary>A field whose value pings <paramref name="target"/> when clicked. The value defaults to its name.</summary>
        public MonitorReport Reference(string label, UnityObject target, string value = null)
        {
            if (!Detailed)
                return this;

            return Add(MonitorLineKind.Reference, label, value ?? (target != null ? target.name : "None"), MonitorTone.Normal, target, 0f);
        }

        /// <summary>A field with a bar beneath it, <paramref name="fraction"/> full.</summary>
        public MonitorReport Meter(string label, float fraction, string value, MonitorTone tone = MonitorTone.Normal)
        {
            return Add(MonitorLineKind.Meter, label, value, tone, null, fraction < 0f ? 0f : fraction > 1f ? 1f : fraction);
        }

        public MonitorReport Note(string text, MonitorTone tone = MonitorTone.Muted)
        {
            return Add(MonitorLineKind.Note, text, null, tone, null, 0f);
        }

        private MonitorReport Add(MonitorLineKind kind, string label, string value, MonitorTone tone, UnityObject target, float fraction)
        {
            if (Detailed)
                lines.Add(new MonitorLine(kind, label, value, tone, target, fraction));

            return this;
        }
    }
}
