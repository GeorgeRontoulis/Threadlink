namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Profiling;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// The Runtime monitor: the Threadlink core and its subsystems as a left-to-right graph of levels with dependency arrows, the selected
    /// subsystem's report, and frame, memory and allocation metrics against a frame budget. Outside Play mode it shows the
    /// subsystems that will deploy.
    /// </summary>
    public sealed class RuntimePage : WorkspacePage, IFillsWorkspace
    {
        private const string CoreKey = "Threadlink";
        private readonly MonitorReport summary = new MonitorReport();
        private readonly MonitorReport detail = new MonitorReport();
        private readonly Queue<float> history = new Queue<float>();
        private RuntimeGraph graph;
        private Label panelName,
            panelCaption,
            hint;
        private ScrollView panelBody;
        private Label fps,
            frame,
            memory,
            allocated;
        private Sparkline spark;
        private string selected = CoreKey;
        private double lastDetail;
        private ProfilerRecorder mainThread,
            totalMemory,
            gcAllocated;

        /// <inheritdoc />
        public override string Title => "Runtime";

        /// <inheritdoc />
        public override string Group => "Runtime";

        /// <inheritdoc />
        public override int Order => 0;

        /// <inheritdoc />
        public override bool RefreshesInPlayMode => true;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.AddToClassList("tl-runtime");
            var body = new VisualElement();
            body.AddToClassList("tl-runtime__body");
            root.Add(body);

            graph = new RuntimeGraph(key =>
            {
                selected = key;
                lastDetail = 0;
                Refresh();
            });
            graph.AddToClassList("tl-graph");
            hint = new Label();
            hint.AddToClassList("tl-graph__hint");
            graph.Add(hint);
            body.Add(graph);

            var panel = new VisualElement();
            panel.AddToClassList("tl-panel");
            var head = new VisualElement();
            head.AddToClassList("tl-panel__head");
            panelName = new Label();
            panelName.AddToClassList("tl-panel__name");
            panelCaption = new Label();
            panelCaption.AddToClassList("tl-panel__caption");
            head.Add(panelName);
            head.Add(panelCaption);
            panel.Add(head);
            panelBody = new ScrollView();
            panelBody.AddToClassList("tl-panel__body");
            panelBody.style.flexGrow = 1;
            panel.Add(panelBody);
            body.Add(panel);

            var strip = new VisualElement();
            strip.AddToClassList("tl-strip");
            fps = Stat(strip, "FPS");
            frame = Stat(strip, "MAIN THREAD");
            memory = Stat(strip, "MEMORY");
            allocated = Stat(strip, "GC / FRAME");
            spark = new Sparkline(history);
            spark.AddToClassList("tl-spark");
            strip.Add(spark);
            root.Add(strip);

            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            totalMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                mainThread.Dispose();
                totalMemory.Dispose();
                gcAllocated.Dispose();
            });
            return root;
        }

        private static Label Stat(VisualElement strip, string label)
        {
            var cell = new VisualElement();
            cell.AddToClassList("tl-stat");
            var caption = new Label(label);
            caption.AddToClassList("tl-stat__label");
            var value = new Label("—");
            value.AddToClassList("tl-stat__value");
            cell.Add(caption);
            cell.Add(value);
            strip.Add(cell);
            return value;
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            if (graph == null)
                return;
            var nodes = new List<RuntimeGraph.Node>();
            bool live = Threadlink.TryGetSingleton(out var core) || (Threadlink.Current != null && EditorApplication.isPlaying);
            core ??= Threadlink.Current;
            if (core != null)
            {
                summary.Begin(false);
                core.Report(summary);
                nodes.Add(new RuntimeGraph.Node(CoreKey, "Threadlink", summary.Caption, true, summary.Alert, Array.Empty<string>()));
                foreach (var subsystem in core.Subsystems)
                {
                    summary.Begin(false);
                    TryReport(subsystem, summary);
                    nodes.Add(
                        new RuntimeGraph.Node(
                            subsystem.GetType().FullName,
                            subsystem.Name,
                            summary.Caption,
                            subsystem.State == SubsystemState.Ready,
                            summary.Alert || subsystem.State == SubsystemState.Failed,
                            subsystem.Dependencies.Select(type => type.FullName).ToArray()
                        )
                    );
                }
                hint.text = live ? "Live session. Select a node for its report." : "Session ended.";
            }
            else
            {
                nodes.Add(new RuntimeGraph.Node(CoreKey, "Threadlink", "Not deployed", false, false, Array.Empty<string>()));
                foreach (var type in DeclaredSubsystems())
                    nodes.Add(new RuntimeGraph.Node(type.FullName, Nicify(type), "Idle", false, false, Array.Empty<string>()));
                hint.text = "Enter Play mode: Threadlink deploys these subsystems before the first scene loads.";
            }
            graph.SetNodes(nodes, selected);
            if (nodes.All(node => node.Key != selected))
                selected = CoreKey;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastDetail >= 0.25 || lastDetail == 0)
            {
                lastDetail = now;
                ShowDetail(core, nodes.First(node => node.Key == selected));
            }
            UpdateStrip();
        }

        private void ShowDetail(Threadlink core, RuntimeGraph.Node node)
        {
            panelName.text = node.Label;
            panelCaption.text = node.Caption ?? string.Empty;
            panelBody.Clear();
            if (core == null)
            {
                Note(node.Key == CoreKey
                    ? "Threadlink owns every subsystem of a session. Subsystems register at SubsystemRegistration, deploy before the first scene's Awake and shut down in reverse order."
                    : "Registered automatically. It initializes when the session deploys, after any subsystem it requires.");
                return;
            }
            detail.Begin(true);
            if (node.Key == CoreKey)
                core.Report(detail);
            else
            {
                var subsystem = core.Subsystems.FirstOrDefault(candidate => candidate.GetType().FullName == node.Key);
                if (subsystem == null)
                    return;
                TryReport(subsystem, detail);
                detail.Section("Lifecycle").Field("State", subsystem.State.ToString(), subsystem.State == SubsystemState.Ready ? MonitorTone.Good : MonitorTone.Bad);
                if (subsystem.Dependencies.Count > 0)
                    detail.Field("Uses", string.Join(", ", subsystem.Dependencies.Select(type => type.Name)), MonitorTone.Muted);
                if (subsystem.Error != null)
                    detail.Note(subsystem.Error.Message, MonitorTone.Bad);
            }
            foreach (var line in detail.Lines)
                Add(line);
        }

        private void Add(MonitorLine line)
        {
            switch (line.Kind)
            {
                case MonitorLineKind.Section:
                    var heading = new Label(line.Label.ToUpperInvariant());
                    heading.AddToClassList("tl-heading");
                    panelBody.Add(heading);
                    break;
                case MonitorLineKind.Note:
                    Note(line.Label, line.Tone);
                    break;
                default:
                    var row = new VisualElement();
                    row.AddToClassList("tl-line");
                    var label = new Label(line.Label);
                    label.AddToClassList("tl-line__label");
                    var value = new Label(line.Value);
                    value.AddToClassList("tl-line__value");
                    value.AddToClassList(ToneClass(line.Tone));
                    row.Add(label);
                    row.Add(value);
                    if (line.Kind == MonitorLineKind.Reference && line.Target != null)
                    {
                        var target = line.Target;
                        value.RegisterCallback<ClickEvent>(_ => EditorGUIUtility.PingObject(target));
                        value.tooltip = "Ping";
                    }
                    panelBody.Add(row);
                    if (line.Kind == MonitorLineKind.Meter)
                    {
                        var meter = new VisualElement();
                        meter.AddToClassList("tl-meter");
                        var fill = new VisualElement();
                        fill.AddToClassList("tl-meter__fill");
                        fill.style.width = Length.Percent(line.Fraction * 100);
                        fill.style.backgroundColor = ThreadlinkTheme.Tone(line.Tone == MonitorTone.Normal ? MonitorTone.Good : line.Tone, ThreadlinkTheme.Light);
                        meter.Add(fill);
                        panelBody.Add(meter);
                    }
                    break;
            }
        }

        private void Note(string text, MonitorTone tone = MonitorTone.Muted)
        {
            var note = new Label(text);
            note.AddToClassList("tl-line__note");
            note.AddToClassList(ToneClass(tone));
            panelBody.Add(note);
        }

        private static string ToneClass(MonitorTone tone) =>
            tone switch
            {
                MonitorTone.Muted => "tl-tone--muted",
                MonitorTone.Good => "tl-tone--good",
                MonitorTone.Caution => "tl-tone--caution",
                MonitorTone.Bad => "tl-tone--bad",
                _ => "tl-tone--normal",
            };

        private static void TryReport(IMonitorable source, MonitorReport report)
        {
            try
            {
                source.Report(report);
            }
            catch (Exception error)
            {
                report.Summary("Report failed", true);
                report.Note(error.Message, MonitorTone.Bad);
            }
        }

        private void UpdateStrip()
        {
            if (!EditorApplication.isPlaying)
            {
                fps.text = frame.text = memory.text = allocated.text = "—";
                history.Clear();
                spark.MarkDirtyRepaint();
                return;
            }
            float ms = mainThread.Valid && mainThread.LastValue > 0 ? mainThread.LastValue / 1_000_000f : Time.unscaledDeltaTime * 1000f;
            float budget = 1000f / (Application.targetFrameRate > 0 ? Application.targetFrameRate : 60f);
            fps.text = Time.unscaledDeltaTime > 0 ? Mathf.RoundToInt(1f / Time.unscaledDeltaTime).ToString() : "—";
            frame.text = ms.ToString("0.0") + " ms";
            frame.style.color = ThreadlinkTheme.Tone(ms > budget ? MonitorTone.Bad : ms > budget * 0.75f ? MonitorTone.Caution : MonitorTone.Good, ThreadlinkTheme.Light);
            memory.text = totalMemory.Valid ? (totalMemory.LastValue / (1024f * 1024f)).ToString("0") + " MB" : "—";
            allocated.text = gcAllocated.Valid ? Bytes(gcAllocated.LastValue) : "—";
            history.Enqueue(ms / budget);
            while (history.Count > 120)
                history.Dequeue();
            spark.MarkDirtyRepaint();
        }

        private static string Bytes(long bytes) =>
            bytes >= 1024 * 1024 ? (bytes / (1024f * 1024f)).ToString("0.0") + " MB"
            : bytes >= 1024 ? (bytes / 1024f).ToString("0.0") + " KB"
            : bytes + " B";

        private static IEnumerable<Type> DeclaredSubsystems() =>
            TypeCache
                .GetTypesDerivedFrom<ThreadlinkSubsystem>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && !Attribute.IsDefined(type, typeof(ManualRegistrationAttribute)))
                .Where(type => !type.Assembly.GetName().Name.EndsWith(".Tests", StringComparison.Ordinal))
                .OrderBy(type => type.Name, StringComparer.Ordinal);

        private static string Nicify(Type type) =>
            type.Name switch
            {
                "ContentLibrary" => "Content",
                "SceneMusicDirector" => "Scene Music",
                _ => ObjectNames.NicifyVariableName(type.Name),
            };

        /// <summary>
        /// The subsystem graph, read left to right like a data flow: the core, then the subsystems that require nothing,
        /// then each subsystem one level after the latest subsystem it requires. Arrows run from a dependency to the
        /// subsystem that uses it; their dashes flow in that direction while the session is live.
        /// </summary>
        private sealed class RuntimeGraph : VisualElement
        {
            internal readonly struct Node
            {
                internal readonly string Key,
                    Label,
                    Caption;
                internal readonly bool Live,
                    Alert;
                internal readonly string[] Dependencies;

                internal Node(string key, string label, string caption, bool live, bool alert, string[] dependencies)
                {
                    Key = key;
                    Label = label;
                    Caption = caption;
                    Live = live;
                    Alert = alert;
                    Dependencies = dependencies;
                }
            }

            private sealed class Card
            {
                internal Node Node;
                internal int Level;
                internal Rect Rect;
                internal Label Text;
            }

            private sealed class Link
            {
                internal Card From,
                    To;
                internal bool FromCore;
                internal float FromOffset,
                    ToOffset;
            }

            private const float CardWidth = 160,
                CardHeight = 46,
                Margin = 24,
                Top = 60,
                Bottom = 40,
                Radius = 7,
                MinimumWidth = 64,
                CompactWidth = 100,
                MinimumGap = 26,
                Dash = 6,
                Gap = 5,
                FlowSpeed = 22;

            private readonly Action<string> select;
            private readonly List<Card> cards = new List<Card>();
            private readonly Dictionary<string, Card> byKey = new Dictionary<string, Card>();
            private readonly List<List<Card>> levels = new List<List<Card>>();
            private readonly List<Link> links = new List<Link>();
            private readonly List<Label> headers = new List<Label>();
            private readonly List<Vector2> curve = new List<Vector2>();
            private readonly Label legend;
            private string structure = string.Empty;
            private string selectedKey;
            private string hovered;
            private IVisualElementScheduledItem animation;

            internal RuntimeGraph(Action<string> select)
            {
                this.select = select;
                generateVisualContent += Draw;
                legend = new Label("Arrows run from a subsystem to the ones that require it. Each level holds the subsystems whose requirements sit to their left.")
                {
                    pickingMode = PickingMode.Ignore,
                };
                legend.AddToClassList("tl-graph__hint");
                legend.style.top = StyleKeyword.Auto;
                legend.style.bottom = 12;
                legend.style.right = 18;
                legend.style.whiteSpace = WhiteSpace.Normal;
                Add(legend);
                RegisterCallback<AttachToPanelEvent>(_ => animation = schedule.Execute(Animate).Every(33));
                RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    animation?.Pause();
                    animation = null;
                });
                RegisterCallback<PointerDownEvent>(evt =>
                {
                    var hit = Hit(evt.localPosition);
                    if (hit != null)
                        select(hit);
                });
                RegisterCallback<PointerMoveEvent>(evt =>
                {
                    var hit = Hit(evt.localPosition);
                    if (hit != hovered)
                    {
                        hovered = hit;
                        MarkDirtyRepaint();
                    }
                });
                RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (hovered == null)
                        return;
                    hovered = null;
                    MarkDirtyRepaint();
                });
                RegisterCallback<GeometryChangedEvent>(_ => Layout());
            }

            internal void SetNodes(List<Node> next, string selection)
            {
                selectedKey = selection;
                string shape = string.Join(";", next.Select(node => node.Key + ":" + string.Join(",", node.Dependencies)));
                if (shape != structure)
                {
                    structure = shape;
                    Rebuild(next);
                }
                else
                    for (int i = 0; i < next.Count; i++)
                        byKey[next[i].Key].Node = next[i];
                Layout();
            }

            // Levels: the core is level 0; a subsystem is one level after the latest subsystem it requires.
            private void Rebuild(List<Node> next)
            {
                foreach (var card in cards)
                    card.Text.RemoveFromHierarchy();
                cards.Clear();
                byKey.Clear();
                links.Clear();
                levels.Clear();
                foreach (var node in next)
                {
                    var text = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
                    text.style.position = Position.Absolute;
                    text.style.unityTextAlign = TextAnchor.MiddleLeft;
                    text.style.whiteSpace = WhiteSpace.Normal;
                    text.style.overflow = Overflow.Hidden;
                    var card = new Card { Node = node, Text = text };
                    cards.Add(card);
                    byKey[node.Key] = card;
                    Add(text);
                }
                if (cards.Count == 0)
                    return;
                var core = cards[0];
                var depth = new Dictionary<Card, int>();
                int Depth(Card card, int guard)
                {
                    if (depth.TryGetValue(card, out int known))
                        return known;
                    int level = 1;
                    if (guard < cards.Count)
                        foreach (var key in card.Node.Dependencies)
                            if (byKey.TryGetValue(key, out var provider) && provider != core)
                                level = Math.Max(level, Depth(provider, guard + 1) + 1);
                    depth[card] = level;
                    return level;
                }
                core.Level = 0;
                foreach (var card in cards.Skip(1))
                    card.Level = Depth(card, 0);
                int deepest = cards.Max(card => card.Level);
                for (int level = 0; level <= deepest; level++)
                    levels.Add(cards.Where(card => card.Level == level).OrderBy(card => card.Node.Label, StringComparer.Ordinal).ToList());
                foreach (var card in cards.Skip(1))
                {
                    bool requiresAny = false;
                    foreach (var key in card.Node.Dependencies)
                        if (byKey.TryGetValue(key, out var provider) && provider != core)
                        {
                            links.Add(new Link { From = provider, To = card });
                            requiresAny = true;
                        }
                    if (!requiresAny)
                        links.Add(new Link { From = core, To = card, FromCore = true });
                }
                Untangle();
                for (int level = 0; level < levels.Count; level++)
                {
                    var header = headers.Count > level ? headers[level] : null;
                    if (header == null)
                    {
                        header = new Label { pickingMode = PickingMode.Ignore };
                        header.AddToClassList("tl-graph__level");
                        header.style.position = Position.Absolute;
                        headers.Add(header);
                        Add(header);
                    }
                    header.text = level == 0 ? "CORE" : deepest == 1 && links.All(link => link.FromCore) ? "SUBSYSTEMS" : "LEVEL " + level;
                    header.style.display = DisplayStyle.Flex;
                }
                for (int level = levels.Count; level < headers.Count; level++)
                    headers[level].style.display = DisplayStyle.None;
            }

            // Orders each level by the average position of its neighbours (a few barycentre sweeps), which removes most
            // crossings in small graphs like this one.
            private void Untangle()
            {
                float Position(Card card) => (levels[card.Level].IndexOf(card) + 0.5f) / levels[card.Level].Count;
                for (int sweep = 0; sweep < 4; sweep++)
                {
                    for (int level = 2; level < levels.Count; level++)
                        Sort(levels[level], card => links.Where(link => link.To == card && !link.FromCore).Select(link => Position(link.From)));
                    for (int level = levels.Count - 2; level >= 1; level--)
                        Sort(levels[level], card => links.Where(link => link.From == card).Select(link => Position(link.To)));
                }
            }

            private static void Sort(List<Card> level, Func<Card, IEnumerable<float>> neighbours)
            {
                var keyed = level
                    .Select((card, index) =>
                    {
                        var around = neighbours(card).ToList();
                        return (card, key: around.Count == 0 ? (index + 0.5f) / level.Count : around.Average());
                    })
                    .OrderBy(entry => entry.key)
                    .ThenBy(entry => entry.card.Node.Label, StringComparer.Ordinal)
                    .Select(entry => entry.card)
                    .ToList();
                level.Clear();
                level.AddRange(keyed);
            }

            private void Layout() => Layout(contentRect.size);

            private void Layout(Vector2 size)
            {
                if (size.x <= 0 || cards.Count == 0)
                    return;
                float available = Mathf.Max(60, size.y - Top - Bottom);
                // Tall levels wrap into extra columns; every column of a level sits before the next level's columns.
                int maxRows = Mathf.Max(1, Mathf.FloorToInt((available + 10) / (CardHeight * 0.75f + 10)));
                var columns = new List<(int level, List<Card> cards)>();
                foreach (var level in levels)
                    for (int start = 0; start < level.Count; start += maxRows)
                        columns.Add((columns.Count == 0 ? 0 : level[0].Level, level.Skip(start).Take(maxRows).ToList()));
                int count = columns.Count;
                // Cards shrink before columns crowd: a gap of at least MinimumGap keeps arrows readable, and below
                // CompactWidth a card shows its name only.
                float margin = size.x < 640 ? 12 : Margin;
                float width = count > 1 ? Mathf.Clamp((size.x - 2 * margin - (count - 1) * MinimumGap) / count, MinimumWidth, CardWidth) : CardWidth;
                float pitch = count > 1 ? Mathf.Min(width + 110, (size.x - 2 * margin - width) / (count - 1)) : 0;
                bool compact = width < CompactWidth;
                float total = (count - 1) * pitch + width;
                float left = Mathf.Max(margin, (size.x - total) * 0.5f);
                int headerColumn = -1;
                for (int column = 0; column < count; column++)
                {
                    var (level, members) = columns[column];
                    float rowPitch = Mathf.Min(CardHeight + 22, available / members.Count);
                    float height = Mathf.Clamp(rowPitch - 10, 30, CardHeight);
                    float top = Top + (available - ((members.Count - 1) * rowPitch + height)) * 0.5f;
                    float x = left + column * pitch;
                    for (int row = 0; row < members.Count; row++)
                    {
                        var card = members[row];
                        card.Rect = new Rect(x, top + row * rowPitch, width, height);
                        var text = card.Text;
                        text.text = "<b>" + card.Node.Label + "</b>" + (compact || string.IsNullOrEmpty(card.Node.Caption) ? string.Empty : "\n<size=10>" + card.Node.Caption + "</size>");
                        text.style.left = card.Rect.x + 18;
                        text.style.top = card.Rect.y;
                        text.style.width = card.Rect.width - 24;
                        text.style.height = card.Rect.height;
                        text.style.color = ThreadlinkTheme.Tone(card.Node.Alert ? MonitorTone.Caution : card.Node.Live ? MonitorTone.Normal : MonitorTone.Muted, ThreadlinkTheme.Light);
                    }
                    if (level != headerColumn && level < headers.Count)
                    {
                        headerColumn = level;
                        headers[level].style.left = x;
                        headers[level].style.top = Top - 24;
                    }
                }
                AssignPorts();
                MarkDirtyRepaint();
            }

            // Spreads the ends of several links along a card's side, ordered by where they go, so they leave and arrive
            // side by side instead of in one bundle.
            private void AssignPorts()
            {
                foreach (var card in cards)
                {
                    Spread(links.Where(link => link.From == card).OrderBy(link => link.To.Rect.center.y).ToList(), card.Rect.height, (link, offset) => link.FromOffset = offset);
                    Spread(links.Where(link => link.To == card).OrderBy(link => link.From.Rect.center.y).ToList(), card.Rect.height, (link, offset) => link.ToOffset = offset);
                }
            }

            private static void Spread(List<Link> ends, float height, Action<Link, float> assign)
            {
                float span = Mathf.Max(0, height * 0.5f - 9);
                for (int i = 0; i < ends.Count; i++)
                    assign(ends[i], ends.Count == 1 ? 0 : Mathf.Lerp(-span, span, i / (float)(ends.Count - 1)));
            }

            private string Hit(Vector2 point)
            {
                foreach (var card in cards)
                    if (card.Rect.Contains(point))
                        return card.Node.Key;
                return null;
            }

            private void Draw(MeshGenerationContext context)
            {
                if (cards.Count == 0 || cards[0].Rect.width <= 0)
                    return;
                bool light = ThreadlinkTheme.Light;
                var painter = context.painter2D;
                Color accent = light ? new Color32(42, 107, 219, 255) : new Color32(90, 158, 255, 255);
                var faint = light ? new Color(0, 0, 0, 0.1f) : new Color(1, 1, 1, 0.08f);
                var flow = light ? new Color(0, 0, 0, 0.38f) : new Color(1, 1, 1, 0.32f);
                float phase = Phase;
                string focus = hovered ?? (selectedKey != cards[0].Node.Key ? selectedKey : null);
                bool Touches(Link link) => focus != null && (link.From.Node.Key == focus || link.To.Node.Key == focus);

                // Unrelated links first, so the focused node's links draw on top.
                foreach (var link in links.Where(link => !Touches(link)))
                {
                    var color = link.FromCore ? faint : flow;
                    if (focus != null)
                        color.a *= 0.35f;
                    DrawLink(painter, link, color, link.FromCore ? 1f : 1.5f, phase);
                }
                foreach (var link in links.Where(Touches))
                    DrawLink(painter, link, link.FromCore ? new Color(accent.r, accent.g, accent.b, 0.5f) : accent, 2f, phase);

                foreach (var card in cards)
                {
                    bool core = card == cards[0];
                    bool related = focus == null || card.Node.Key == focus || links.Any(link => Touches(link) && (link.From == card || link.To == card));
                    Color fill = card.Node.Alert
                        ? ThreadlinkTheme.Tone(MonitorTone.Caution, light)
                        : card.Node.Live
                            ? (light ? new Color(0.985f, 0.985f, 0.99f) : new Color(0.13f, 0.13f, 0.15f))
                            : (light ? new Color(0.93f, 0.93f, 0.95f) : new Color(0.09f, 0.09f, 0.1f));
                    if (!related)
                        fill.a = 0.55f;
                    painter.fillColor = fill;
                    RoundedRect(painter, card.Rect, Radius);
                    painter.Fill();
                    painter.lineWidth = card.Node.Key == selectedKey ? 2.5f : card.Node.Key == hovered ? 2f : core ? 1.5f : 1f;
                    painter.strokeColor = card.Node.Key == selectedKey ? accent : core && card.Node.Live ? accent : card.Node.Live ? ThreadlinkTheme.Tone(MonitorTone.Good, light) : faint;
                    RoundedRect(painter, card.Rect, Radius);
                    painter.Stroke();
                    // State dot: good while ready, caution on alerts, muted when idle.
                    painter.fillColor = ThreadlinkTheme.Tone(card.Node.Alert ? MonitorTone.Caution : card.Node.Live ? MonitorTone.Good : MonitorTone.Muted, light);
                    painter.BeginPath();
                    painter.Arc(new Vector2(card.Rect.x + 10, card.Rect.center.y), 3.5f, 0, 360);
                    painter.Fill();
                }
            }

            private void DrawLink(Painter2D painter, Link link, Color color, float width, float phase)
            {
                var start = new Vector2(link.From.Rect.xMax, link.From.Rect.center.y + link.FromOffset);
                var end = new Vector2(link.To.Rect.xMin, link.To.Rect.center.y + link.ToOffset);
                float reach = Mathf.Max(30, (end.x - start.x) * 0.5f);
                Bezier(start, start + new Vector2(reach, 0), end - new Vector2(reach, 0), end);
                bool live = link.From.Node.Live && link.To.Node.Live;
                painter.lineWidth = width;
                Dashed(painter, curve, color, live ? phase : 0);
                if (!link.FromCore)
                    Arrow(painter, curve[curve.Count - 2], end, color);
            }

            private void Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                curve.Clear();
                const int Steps = 24;
                for (int i = 0; i <= Steps; i++)
                {
                    float t = i / (float)Steps,
                        u = 1 - t;
                    curve.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
                }
            }

            // Whether relationship lines should move: only while the session is live and the graph is shown.
            private bool Animating => EditorApplication.isPlaying && cards.Count > 0 && cards[0].Node.Live;

            private float Phase => Animating ? (float)(EditorApplication.timeSinceStartup * FlowSpeed % (Dash + Gap)) : 0;

            private void Animate()
            {
                if (Animating && panel != null && resolvedStyle.display != DisplayStyle.None)
                    MarkDirtyRepaint();
            }

            /// <summary>Dashes along a polyline; they move from its first point toward its last as the phase grows.</summary>
            private static void Dashed(Painter2D painter, List<Vector2> points, Color color, float phase)
            {
                painter.strokeColor = color;
                painter.BeginPath();
                float travelled = 0;
                float next = phase - (Dash + Gap);
                for (int i = 1; i < points.Count; i++)
                {
                    var a = points[i - 1];
                    var b = points[i];
                    float length = Vector2.Distance(a, b);
                    if (length < 0.001f)
                        continue;
                    // Emit every dash that overlaps this segment.
                    while (next < travelled + length)
                    {
                        float from = Mathf.Max(next, travelled),
                            to = Mathf.Min(next + Dash, travelled + length);
                        if (to > from)
                        {
                            painter.MoveTo(Vector2.Lerp(a, b, (from - travelled) / length));
                            painter.LineTo(Vector2.Lerp(a, b, (to - travelled) / length));
                        }
                        if (next + Dash > travelled + length)
                            break;
                        next += Dash + Gap;
                    }
                    travelled += length;
                }
                painter.Stroke();
            }

            /// <summary>A small arrowhead at the end of a link.</summary>
            private static void Arrow(Painter2D painter, Vector2 from, Vector2 tip, Color color)
            {
                var direction = (tip - from).normalized;
                var side = new Vector2(-direction.y, direction.x) * 3.5f;
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(tip);
                painter.LineTo(tip - direction * 8 + side);
                painter.LineTo(tip - direction * 8 - side);
                painter.ClosePath();
                painter.Fill();
            }

            private static void RoundedRect(Painter2D painter, Rect rect, float radius)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
                painter.ArcTo(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMin + radius), radius);
                painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
                painter.ArcTo(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - radius, rect.yMax), radius);
                painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
                painter.ArcTo(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMax - radius), radius);
                painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
                painter.ArcTo(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin + radius, rect.yMin), radius);
                painter.ClosePath();
            }
        }

        /// <summary>Frame time history against the budget; 1.0 is the budget line.</summary>
        private sealed class Sparkline : VisualElement
        {
            private readonly Queue<float> history;

            internal Sparkline(Queue<float> history)
            {
                this.history = history;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || history.Count < 2)
                    return;
                bool light = ThreadlinkTheme.Light;
                var painter = context.painter2D;
                float top = 10,
                    bottom = rect.height - 10;
                float Y(float value) => Mathf.Lerp(bottom, top, Mathf.Clamp01(value / 1.5f));
                painter.lineWidth = 1;
                painter.strokeColor = light ? new Color(0, 0, 0, 0.2f) : new Color(1, 1, 1, 0.15f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(12, Y(1)));
                painter.LineTo(new Vector2(rect.width - 12, Y(1)));
                painter.Stroke();
                painter.lineWidth = 1.5f;
                float worst = history.Max();
                painter.strokeColor = ThreadlinkTheme.Tone(worst > 1 ? MonitorTone.Bad : worst > 0.75f ? MonitorTone.Caution : MonitorTone.Good, light);
                painter.BeginPath();
                int i = 0;
                float step = (rect.width - 24) / 119f;
                foreach (float value in history)
                {
                    var point = new Vector2(12 + step * (120 - history.Count + i), Y(value));
                    if (i++ == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }
                painter.Stroke();
            }
        }
    }
}
