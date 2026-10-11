# Monitoring

Subsystems and services describe their own state through one contract, `IMonitorable`. The [Runtime Monitor](editor-tooling.md#runtime-monitor) shows whatever they report, and knows none of them by name: native and project code are monitored the same way.

The types are in `Threadlink.Shared`.

## Reports

```csharp
public interface IMonitorable
{
    void Report(MonitorReport report);
}
```

A report has two parts:

- **A summary**, always: `report.Summary(caption, alert)`. The caption is the short status on the node, such as `"4 linked"` or `"Ready"`; `alert` puts a red dot on it. Summaries are requested ten times a second, so keep them cheap.
- **Details**, only while `report.Detailed` is `true`, when the node is selected. Add lines in order:

| Method | Shows |
|---|---|
| `Section(title)` | A heading. |
| `Field(label, value, tone)` | A label and a value. Overloads take `string`, `int`, `long`, `bool` and `float` (with an optional format). |
| `Reference(label, unityObject, value)` | A value that pings the object when clicked. The value defaults to the object's name. |
| `Meter(label, fraction, value, tone)` | A field with a bar, `fraction` full (0–1). |
| `Note(text, tone)` | A wrapped line of text. |

`MonitorTone` is `Normal`, `Muted`, `Good`, `Caution` or `Bad`. Each method returns the report, so calls chain. Lines added while `Detailed` is `false` are ignored, but return early anyway, to avoid building strings nobody reads.

Reporting costs nothing unless a monitor asks. A reporter that throws is shown as failed; it never breaks the monitor.

## Subsystems

Every `ThreadlinkSubsystem<T>` is `IMonitorable`, with an empty virtual `Report`. A `Register`, `Linker` or `Weaver` reports its linked objects by default. Override `Report` to describe your own state:

```csharp
[Monitored(Description = "Tracks the player's inventory.")]
public sealed class InventorySystem : ThreadlinkSubsystem<InventorySystem>
{
    private const int Capacity = 40;
    private readonly List<Item> items = new();

    public override void Report(MonitorReport report)
    {
        report.Summary(items.Count + " items", items.Count > Capacity);

        if (!report.Detailed)
            return;

        report.Section("Items")
            .Field("Count", items.Count)
            .Meter("Capacity", (float)items.Count / Capacity, items.Count + " of " + Capacity);
    }
}
```

A register keeps its link list by calling the protected `ReportLinks(report)` from its override, and `LinkedCountCaption()` gives the default caption.

`[Monitored]` on a subsystem class is optional. Its `Description` is shown while the subsystem isn't running, and its `Order` sorts the subsystems shown before deployment. Once deployed, subsystems appear in weave order.

## Static services

A static class has no instance to implement an interface, so it marks a static report method instead:

```csharp
public static class Matchmaking
{
    public enum State { Idle, Searching, Joined, Failed }

    public static State Current { get; private set; }

    [Monitored("Matchmaking", Description = "Finds and joins sessions.")]
    private static void Report(MonitorReport report)
    {
        report.Summary(Current.ToString(), Current == State.Failed);

        if (report.Detailed)
            report.Section("Session").Field("State", Current.ToString());
    }
}
```

The method must be `static void` with one `MonitorReport` parameter; it may be private. The name defaults to the class name. Iris, Nexus, Initium and Scribe are monitored this way. Nodes sort by `Order`, then name; Threadlink's own use negative orders.

## Other objects

An object that is neither a subsystem nor a static service, such as a scene singleton or a network session, implements `IMonitorable` and registers itself. Here, `IDiscoverable` makes Initium boot and discard the object with its scene:

```csharp
public sealed class Session : LinkableBehaviourSingleton<Session>, IDiscoverable, IMonitorable
{
    public int PlayerCount { get; private set; }

    public override void Boot()
    {
        base.Boot();
        MonitorRegistry.Register("Session", this);
    }

    public override void Discard()
    {
        MonitorRegistry.Unregister(this);
        base.Discard();
    }

    public void Report(MonitorReport report) => report.Summary(PlayerCount + " players");
}
```

Registering the same object again replaces its entry. The registry clears when Play Mode exits, and destroyed Unity objects are skipped.
