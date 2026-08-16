namespace Threadlink.Editor.CodeGen
{
    using Cysharp.Text;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;

    internal enum ThreadlinkDomainChangeKind : byte
    {
        Added,
        Removed,
        Renamed,
        Rescoped,
        Collision
    }

    internal readonly struct ThreadlinkDomainChange
    {
        internal ThreadlinkDomainChangeKind Kind { get; }
        internal string Subject { get; }
        internal string Detail { get; }

        internal ThreadlinkDomainChange(ThreadlinkDomainChangeKind kind, string subject, string detail)
        {
            Kind = kind;
            Subject = subject;
            Detail = detail;
        }
    }

    internal sealed class ThreadlinkDomainDiff
    {
        internal string DomainName { get; }
        internal List<ThreadlinkDomainChange> Changes { get; } = new(1);

        internal bool HasBlockingIssues { get; private set; }

        internal ThreadlinkDomainDiff(string domainName) => DomainName = domainName;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Record(ThreadlinkDomainChangeKind kind, string subject, string detail)
        {
            Changes.Add(new ThreadlinkDomainChange(kind, subject, detail));

            if (kind is ThreadlinkDomainChangeKind.Collision)
                HasBlockingIssues = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasChanges() => Changes.Count > 0;

        internal void Report()
        {
            int count = Changes.Count;

            if (count <= 0)
                return;

            using var builder = ZString.CreateStringBuilder();

            builder.Append("Domain '");
            builder.Append(DomainName);
            builder.Append("' changed:");

            for (int i = 0; i < count; i++)
            {
                var change = Changes[i];

                builder.AppendLine();
                builder.Append("  [");
                builder.Append(change.Kind.ToString());
                builder.Append("] ");
                builder.Append(change.Subject);

                if (string.IsNullOrEmpty(change.Detail))
                    continue;

                builder.Append(" - ");
                builder.Append(change.Detail);
            }

            Scribe.Send<ThreadlinkDomainDiff>(builder.ToString())
            .ToUnityConsole(HasBlockingIssues ? DebugType.Error : DebugType.Info);
        }
    }
}
