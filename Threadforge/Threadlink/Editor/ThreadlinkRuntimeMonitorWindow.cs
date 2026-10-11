namespace Threadlink.Editor
{
    using Core;
    using Core.NativeSubsystems.Aura;
    using Core.NativeSubsystems.Chronos;
    using Core.NativeSubsystems.Dextra;
    using Core.NativeSubsystems.Iris;
    using Core.NativeSubsystems.Nexus;
    using Core.NativeSubsystems.Sentinel;
    using Generated;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Unity.Profiling;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Profiling;
    using UnityEngine.SceneManagement;
    using Theme = ThreadlinkEditorTheme;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// A live view of Threadlink's runtime: the core and everything around it as a radial graph, the selected node's
    /// state, and frame, memory and rendering metrics against a frame budget. Replaces the Registers Tracker.
    /// </summary>
    internal sealed class ThreadlinkRuntimeMonitorWindow : EditorWindow
    {
        #region Model:
        private enum NodeKind : byte { Core, Woven, Service }
        private enum RuntimeState : byte { Offline, Waiting, Deploying, Live }

        private sealed class Node
        {
            public string Key;
            public string Label;
            public NodeKind Kind;
            public Type Type;
            public IThreadlinkSubsystem Subsystem;
            public bool Live;
            public bool Alert;
            public string Caption;
            public Vector2 Centre;
            public float Radius;
            public int Index;
        }

        private const string CoreKey = "Threadlink";
        private const string IrisKey = "Iris", NexusKey = "Nexus", InitiumKey = "Initium", ScribeKey = "Scribe";
        private static readonly string[] ServiceKeys = { IrisKey, NexusKey, InitiumKey, ScribeKey };
        private static readonly Type[] NativeWoven = { typeof(Sentinel), typeof(Chronos), typeof(Dextra), typeof(Aura) };
        private static readonly int[] TargetFrameRates = { 0, 30, 60, 90, 120, 144, 240 };

        private static List<Type> declaredUserSubsystems;
        private static ThreadlinkIDs.Iris.Events[] irisEvents;
        private static readonly Regex RichText = new("<.*?>", RegexOptions.Compiled);
        #endregion

        #region Palette and layout:
        private static readonly Color Background = new(0.018f, 0.018f, 0.022f);
        private static readonly Color PanelBackground = new(0.05f, 0.05f, 0.058f);
        private static readonly Color StripBackground = new(0.032f, 0.032f, 0.038f);
        private static readonly Color Rule = new(1f, 1f, 1f, 0.07f);
        private static readonly Color Ink = new(0.95f, 0.95f, 0.95f);
        private static readonly Color InkDim = new(1f, 1f, 1f, 0.58f);
        private static readonly Color InkFaint = new(1f, 1f, 1f, 0.32f);
        private static readonly Color NodeInk = new(0.05f, 0.05f, 0.06f);
        private static readonly Color Edge = new(1f, 1f, 1f, 0.3f);
        private static readonly Color EdgeIdle = new(1f, 1f, 1f, 0.1f);
        private static readonly Color IdleFill = new(0.085f, 0.085f, 0.095f);
        private static readonly Color IdleStroke = new(1f, 1f, 1f, 0.16f);
        private static readonly Color Good = new(0.38f, 0.85f, 0.56f);
        private static readonly Color Caution = new(0.98f, 0.73f, 0.3f);
        private static readonly Color Bad = new(1f, 0.4f, 0.4f);

        private const float PanelWidth = 320f;
        private const float StripHeight = 64f;
        private const float PanelPadding = 18f;
        private const float CoreRadius = 50f;
        private const float NodeRadius = 35f;
        private const int HistoryLength = 120;
        #endregion

        #region State:
        [SerializeField] private string selectedKey = CoreKey;
        [SerializeField] private int targetFrameRateIndex;
        [SerializeField] private Vector2 panelScroll;

        private readonly List<Node> nodes = new(16);
        private readonly Dictionary<string, Node> nodeCache = new(16);
        private Node coreNode;
        private Threadlink core;
        private RuntimeState state;

        private double lastSample, lastRepaint;
        private string hoveredKey, pendingKey;

        // Frame and memory metrics, sampled ten times a second in Play Mode.
        private ProfilerRecorder mainThread, gcAllocInFrame, drawCalls, batches, setPassCalls, triangles, systemMemory;
        private float fps, mainThreadMs, gcPerFrame;
        private int lastFrameCount, gcBaseline;
        private double lastFrameTime;
        private readonly float[] history = new float[HistoryLength];
        private int historyCount;

        // Console activity, captured on any thread.
        private readonly object logLock = new();
        private readonly Queue<(LogType Type, string Text)> recentMessages = new(8);
        private int logCount, warningCount, errorCount;
        private volatile bool capturing;
        private (int Logs, int Warnings, int Errors) scribeCounts;
        private (LogType Type, string Text)[] scribeSnapshot;

        // Per-node data refreshed with the graph.
        private readonly List<(string Event, int Count)> irisListeners = new(32);
        private int irisTotal;
        private readonly List<(string Scene, int Count)> discoverables = new(8);
        private double lastDiscoverableScan = -10d;

        private readonly Vector3[] segment = new Vector3[2];
        private readonly Vector3[] arrow = new Vector3[3];
        private readonly Vector3[] spark = new Vector3[HistoryLength];
        private readonly List<IThreadlinkSubsystem> weaveOrder = new(16);
        private readonly List<KeyValuePair<int, IIdentifiable>> registryBuffer = new(64);

        private GUIStyle nodeStyle, coreStyle, coreCaptionStyle, captionStyle, headerStyle, chipStyle, legendStyle;
        private GUIStyle titleStyle, subtitleStyle, sectionStyle, keyStyle, valueStyle, noteStyle;
        private GUIStyle metricLabelStyle, metricValueStyle, metricNoteStyle;
        #endregion

        [MenuItem("Threadlink/Runtime Monitor", priority = 21)]
        internal static void Open()
        {
            var window = GetWindow<ThreadlinkRuntimeMonitorWindow>();
            window.titleContent = new GUIContent("Runtime Monitor");
            window.minSize = new Vector2(820f, 520f);
            window.Show();
        }

        #region Lifecycle:
        private void OnEnable()
        {
            wantsMouseMove = true;

            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");

            capturing = EditorApplication.isPlayingOrWillChangePlaymode;
            gcBaseline = GC.CollectionCount(0);

            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Application.logMessageReceivedThreaded -= OnLogMessage;

            mainThread.Dispose();
            gcAllocInFrame.Dispose();
            drawCalls.Dispose();
            batches.Dispose();
            setPassCalls.Dispose();
            triangles.Dispose();
            systemMemory.Dispose();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                // Before the domain reloads, if it does, so the session's first messages are counted.
                case PlayModeStateChange.ExitingEditMode:
                    lock (logLock)
                    {
                        logCount = warningCount = errorCount = 0;
                        recentMessages.Clear();
                    }

                    fps = mainThreadMs = gcPerFrame = 0f;
                    historyCount = 0;
                    lastFrameTime = 0d;
                    gcBaseline = GC.CollectionCount(0);
                    capturing = true;
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    capturing = false;
                    break;
            }

            Repaint();
        }

        /// <summary>Samples metrics at 10 Hz and animates the graph at 30 Hz, only in Play Mode.</summary>
        private void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;

            if (now - lastSample >= 0.1d)
            {
                Sample(now);
                lastSample = now;
            }

            if (now - lastRepaint >= 1d / 30d)
            {
                lastRepaint = now;
                Repaint();
            }
        }

        private void Sample(double now)
        {
            int frame = Time.frameCount;

            if (lastFrameTime > 0d && now > lastFrameTime)
            {
                float instant = (float)((frame - lastFrameCount) / (now - lastFrameTime));
                fps = fps <= 0f ? instant : Mathf.Lerp(fps, instant, 0.35f);
            }

            lastFrameCount = frame;
            lastFrameTime = now;

            mainThreadMs = (float)(Average(mainThread) * 1e-6);
            gcPerFrame = (float)Average(gcAllocInFrame);

            if (EditorApplication.isPaused)
                return;

            if (historyCount < HistoryLength)
            {
                history[historyCount++] = mainThreadMs;
            }
            else
            {
                Array.Copy(history, 1, history, 0, HistoryLength - 1);
                history[HistoryLength - 1] = mainThreadMs;
            }
        }

        private static double Average(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0)
                return 0d;

            double sum = 0d;
            int count = recorder.Count;

            for (int i = 0; i < count; i++)
                sum += recorder.GetSample(i).Value;

            return sum / count;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!capturing)
                return;

            lock (logLock)
            {
                switch (type)
                {
                    case LogType.Warning: warningCount++; break;
                    case LogType.Error:
                    case LogType.Exception:
                    case LogType.Assert: errorCount++; break;
                    default: logCount++; break;
                }

                // Scribe prefixes every message with the sender's name in cyan.
                if (condition == null || !condition.StartsWith("<color=cyan>[", StringComparison.Ordinal))
                    return;

                string text = RichText.Replace(condition, string.Empty);
                int newline = text.IndexOf('\n');

                if (newline >= 0)
                    text = text.Substring(0, newline);

                if (text.Length > 180)
                    text = text.Substring(0, 180) + "…";

                if (recentMessages.Count >= 6)
                    recentMessages.Dequeue();

                recentMessages.Enqueue((type, text));
            }
        }
        #endregion

        #region Graph model:
        private void RefreshModel()
        {
            core = null;

            if (!EditorApplication.isPlaying)
                state = RuntimeState.Offline;
            else if (!Threadlink.TryGetSingleton(out core))
                state = RuntimeState.Waiting;
            else
                state = core.IsDeployed ? RuntimeState.Live : RuntimeState.Deploying;

            nodes.Clear();
            weaveOrder.Clear();

            coreNode = GetNode(CoreKey, "Threadlink", NodeKind.Core, typeof(Threadlink));
            coreNode.Live = core != null;
            coreNode.Subsystem = core;

            if (core != null)
            {
                foreach (var subsystem in core.EditorOnly_WeaveOrder)
                {
                    if (subsystem == null)
                        continue;

                    weaveOrder.Add(subsystem);

                    var type = subsystem.GetType();
                    var node = GetNode(type.FullName, type.Name, NodeKind.Woven, type);

                    node.Subsystem = subsystem;
                    node.Live = IsLinked(subsystem);
                    nodes.Add(node);
                }
            }

            // Before deployment, show the native subsystems and the user subsystems declared in the project.
            if (nodes.Count == 0)
            {
                foreach (var type in NativeWoven)
                    nodes.Add(GetIdleNode(type));

                foreach (var type in DeclaredUserSubsystems())
                    nodes.Add(GetIdleNode(type));
            }

            foreach (string key in ServiceKeys)
            {
                var node = GetNode(key, key, NodeKind.Service, null);
                node.Live = EditorApplication.isPlaying;
                nodes.Add(node);
            }

            RefreshIris();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.Index = i;
                RefreshCaption(node);
            }

            RefreshCaption(coreNode);
        }

        /// <summary>Whether the core links <paramref name="subsystem"/>; false while the core is shutting down.</summary>
        private bool IsLinked(IThreadlinkSubsystem subsystem)
        {
            try
            {
                return core != null && core.HasLinked(subsystem.ID);
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private void RefreshCaption(Node node)
        {
            try
            {
                node.Caption = Caption(node);
                node.Alert = Alert(node);
            }
            catch (Exception)
            {
                // A subsystem mid-deployment or mid-shutdown can be in any state.
                node.Caption = "—";
                node.Alert = false;
            }
        }

        private Node GetIdleNode(Type type)
        {
            var node = GetNode(type.FullName, type.Name, NodeKind.Woven, type);
            node.Subsystem = null;
            node.Live = false;
            return node;
        }

        private Node GetNode(string key, string label, NodeKind kind, Type type)
        {
            if (!nodeCache.TryGetValue(key, out var node))
            {
                node = new Node { Key = key };
                nodeCache[key] = node;
            }

            node.Label = label;
            node.Kind = kind;
            node.Type = type;
            return node;
        }

        private static List<Type> DeclaredUserSubsystems()
        {
            if (declaredUserSubsystems != null)
                return declaredUserSubsystems;

            declaredUserSubsystems = new List<Type>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<IThreadlinkSubsystem>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters || type == typeof(Threadlink)
                || Array.IndexOf(NativeWoven, type) >= 0)
                    continue;

                declaredUserSubsystems.Add(type);
            }

            declaredUserSubsystems.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return declaredUserSubsystems;
        }

        private void RefreshIris()
        {
            irisListeners.Clear();
            irisTotal = 0;

            if (!EditorApplication.isPlaying)
                return;

            irisEvents ??= (ThreadlinkIDs.Iris.Events[])Enum.GetValues(typeof(ThreadlinkIDs.Iris.Events));

            foreach (var eventID in irisEvents)
            {
                if (!Iris.TryGetListenerCount(eventID, out int count) || count <= 0)
                    continue;

                irisListeners.Add((eventID.ToString(), count));
                irisTotal += count;
            }

            irisListeners.Sort((a, b) => b.Count.CompareTo(a.Count));
        }

        private string Caption(Node node)
        {
            switch (node.Kind)
            {
                case NodeKind.Core:
                    return state switch
                    {
                        RuntimeState.Live => weaveOrder.Count + " woven",
                        RuntimeState.Deploying => "deploying",
                        RuntimeState.Waiting => "waiting",
                        _ => "offline",
                    };

                case NodeKind.Service:
                    if (!node.Live)
                        return string.Empty;

                    switch (node.Key)
                    {
                        case IrisKey: return irisTotal + " listeners";
                        case NexusKey: return Count(Nexus.EditorOnly_Scenes) + " scenes";
                        case ScribeKey:
                            lock (logLock)
                                return errorCount > 0 ? errorCount + (errorCount == 1 ? " error" : " errors") : logCount + " logs";
                        default: return "pipeline";
                    }

                default:
                    if (!node.Live)
                        return EditorApplication.isPlaying && state == RuntimeState.Live ? "not woven" : string.Empty;

                    switch (node.Subsystem)
                    {
                        case Sentinel sentinel: return sentinel.State.ToString();
                        case Chronos _: return Chronos.DeltaTime > 0f ? Mathf.RoundToInt(1f / Chronos.DeltaTime) + " fps" : "—";
                        case Dextra dextra: return dextra.CurrentInputDevice.ToString();
                        case IEditorOnlyRegister register: return register.EditorOnly_Count + " linked";
                        default: return "woven";
                    }
            }
        }

        private bool Alert(Node node)
        {
            if (!node.Live)
                return false;

            if (node.Subsystem is Sentinel sentinel)
                return sentinel.State is Sentinel.DeploymentState.Failed;

            if (node.Key == ScribeKey)
            {
                lock (logLock)
                    return errorCount > 0;
            }

            return false;
        }

        private static int Count<T>(IEnumerable<T> items)
        {
            int count = 0;

            foreach (var _ in items)
                count++;

            return count;
        }
        #endregion

        #region GUI:
        private void BuildStyles()
        {
            if (nodeStyle != null)
                return;

            nodeStyle = Theme.Label(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            coreStyle = Theme.Label(13, FontStyle.Bold, TextAnchor.MiddleCenter);
            coreCaptionStyle = Theme.Label(9, FontStyle.Normal, TextAnchor.MiddleCenter);
            captionStyle = Theme.Label(10, FontStyle.Normal, TextAnchor.MiddleCenter);
            headerStyle = Theme.Label(11, FontStyle.Bold);
            chipStyle = Theme.Label(9, FontStyle.Bold, TextAnchor.MiddleLeft);
            legendStyle = Theme.Label(10);
            titleStyle = Theme.Label(18, FontStyle.Bold);
            subtitleStyle = Theme.Label(10);
            sectionStyle = Theme.Label(9, FontStyle.Bold);
            keyStyle = Theme.Label(11);
            valueStyle = Theme.Label(11, FontStyle.Normal, TextAnchor.MiddleRight);
            noteStyle = Theme.Label(10);
            noteStyle.wordWrap = true;
            metricLabelStyle = Theme.Label(9, FontStyle.Bold);
            metricValueStyle = Theme.Label(15, FontStyle.Bold);
            metricNoteStyle = Theme.Label(9);
        }

        private void OnGUI()
        {
            BuildStyles();

            if (Event.current.type == EventType.Layout || nodes.Count == 0)
                RefreshModel();

            var graphRect = new Rect(0f, 0f, position.width - PanelWidth, position.height - StripHeight);
            var panelRect = new Rect(graphRect.xMax, 0f, PanelWidth, position.height - StripHeight);
            var stripRect = new Rect(0f, graphRect.yMax, position.width, StripHeight);

            Theme.Fill(new Rect(0f, 0f, position.width, position.height), Background);

            LayoutGraph(graphRect);
            HandleGraphInput(graphRect);
            DrawGraph(graphRect);
            DrawPanel(panelRect);
            DrawStrip(stripRect);

            // A click selects after drawing, so the panel drawn matches its Layout pass.
            if (pendingKey != null)
            {
                selectedKey = pendingKey;
                pendingKey = null;
                panelScroll = Vector2.zero;
                GUIUtility.keyboardControl = 0;
                Repaint();
            }

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void LayoutGraph(Rect rect)
        {
            var centre = rect.center + new Vector2(0f, 8f);
            int count = nodes.Count;

            float ring = Mathf.Max(90f, Mathf.Min(rect.width * 0.5f - NodeRadius - 84f, rect.height * 0.5f - NodeRadius - 40f));
            float nodeRadius = count > 1 ? Mathf.Clamp(ring * Mathf.Sin(Mathf.PI / count) - 10f, 18f, NodeRadius) : NodeRadius;

            coreNode.Centre = centre;
            coreNode.Radius = Mathf.Min(CoreRadius, ring * 0.42f);

            for (int i = 0; i < count; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / count;
                var node = nodes[i];

                node.Centre = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                node.Radius = nodeRadius;
            }
        }

        private void HandleGraphInput(Rect rect)
        {
            var current = Event.current;
            var hovered = NodeAt(current.mousePosition);

            hoveredKey = rect.Contains(current.mousePosition) ? hovered?.Key : null;

            if (hovered != null)
                EditorGUIUtility.AddCursorRect(Theme.Circle(hovered.Centre, hovered.Radius), MouseCursor.Link);

            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                pendingKey = hovered != null ? hovered.Key : CoreKey;
                current.Use();
            }
        }

        private Node NodeAt(Vector2 point)
        {
            if (Vector2.Distance(point, coreNode.Centre) <= coreNode.Radius)
                return coreNode;

            foreach (var node in nodes)
            {
                if (Vector2.Distance(point, node.Centre) <= node.Radius + 2f)
                    return node;
            }

            return null;
        }

        private void DrawGraph(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var centre = coreNode.Centre;
            bool animate = state == RuntimeState.Live && !EditorApplication.isPaused;
            float time = (float)(EditorApplication.timeSinceStartup % 1000d);

            // A faint orbit through the ring.
            if (nodes.Count > 0)
            {
                float ring = Vector2.Distance(nodes[0].Centre, centre);
                Theme.StrokeRounded(Theme.Circle(centre, ring), new Color(1f, 1f, 1f, 0.035f), ring, 1f);
            }

            // Spokes, with arrows into the core and pulses travelling along live ones.
            foreach (var node in nodes)
            {
                var direction = (centre - node.Centre).normalized;
                var start = node.Centre + direction * (node.Radius + 5f);
                var end = centre - direction * (coreNode.Radius + 7f);
                bool live = node.Live && coreNode.Live;

                Handles.color = live ? Edge : EdgeIdle;
                segment[0] = start;
                segment[1] = end - direction * 6f;
                Handles.DrawAAPolyLine(1.4f, segment);

                var perpendicular = new Vector2(-direction.y, direction.x) * 3.6f;
                var arrowBase = end - direction * 8f;
                arrow[0] = end;
                arrow[1] = arrowBase + perpendicular;
                arrow[2] = arrowBase - perpendicular;
                Handles.DrawAAConvexPolygon(arrow);

                if (animate && live)
                {
                    float t = Mathf.Repeat(time * 0.45f + node.Index * 0.161f, 1f);
                    var pulse = Vector2.Lerp(start, arrowBase, t * t);
                    Theme.FillRounded(Theme.Circle(pulse, 2.2f), Theme.WithAlpha(Ink, Mathf.Sin(t * Mathf.PI) * 0.75f), 2.2f);
                }
            }

            Handles.color = Color.white;

            foreach (var node in nodes)
                DrawNode(node, centre);

            DrawNode(coreNode, centre);
            DrawGraphHeader(rect);
            DrawLegend(rect);
        }

        private void DrawNode(Node node, Vector2 graphCentre)
        {
            var rect = Theme.Circle(node.Centre, node.Radius);
            bool selected = node.Key == selectedKey;
            bool hovered = node.Key == hoveredKey;

            if (selected || hovered)
            {
                float halo = node.Radius + 6f;
                Theme.StrokeRounded(Theme.Circle(node.Centre, halo), Theme.WithAlpha(Ink, selected ? 0.85f : 0.3f), halo, 1.4f);
            }

            Color ink;

            if (!node.Live)
            {
                Theme.FillRounded(rect, IdleFill, node.Radius);
                Theme.StrokeRounded(rect, IdleStroke, node.Radius, 1f);
                ink = InkDim;
            }
            else if (node.Kind == NodeKind.Service)
            {
                Theme.FillRounded(rect, Background, node.Radius);
                Theme.StrokeRounded(rect, Ink, node.Radius, 1.5f);
                ink = Ink;
            }
            else
            {
                Theme.FillRounded(rect, Ink, node.Radius);
                ink = NodeInk;
            }

            if (node.Kind == NodeKind.Core)
            {
                Theme.Draw(new Rect(rect.x, node.Centre.y - 13f, rect.width, 18f), node.Label, coreStyle, ink);
                Theme.Draw(new Rect(rect.x, node.Centre.y + 5f, rect.width, 12f), node.Caption, coreCaptionStyle, Theme.WithAlpha(ink, 0.55f));
                return;
            }

            var style = nodeStyle;
            style.fontSize = node.Radius < 28f ? 9 : 11;
            float width = node.Radius * 2f - 8f;

            Theme.Draw(new Rect(rect.x + 4f, node.Centre.y - 9f, width, 18f), Theme.Fit(node.Label, style, width), style, ink);

            if (node.Alert)
            {
                var dot = node.Centre + new Vector2(node.Radius * 0.72f, -node.Radius * 0.72f);
                Theme.FillRounded(Theme.Circle(dot, 5.5f), Background, 5.5f);
                Theme.FillRounded(Theme.Circle(dot, 4f), Bad, 4f);
            }

            // The caption sits outside the ring, away from the spoke.
            if (!string.IsNullOrEmpty(node.Caption))
            {
                var outward = (node.Centre - graphCentre).normalized;
                float textWidth = Theme.Measure(node.Caption, captionStyle).x;
                var anchor = node.Centre + outward * (node.Radius + 10f) + new Vector2(outward.x * textWidth * 0.5f, outward.y * 4f);

                Theme.Draw(new Rect(anchor.x - textWidth * 0.5f - 2f, anchor.y - 7f, textWidth + 4f, 14f), node.Caption, captionStyle,
                    node.Alert ? Bad : InkDim);
            }
        }

        private void DrawGraphHeader(Rect rect)
        {
            Theme.Draw(new Rect(rect.x + 18f, rect.y + 14f, 160f, 18f), "Runtime Monitor", headerStyle, InkDim);

            (string label, Color color) = state switch
            {
                RuntimeState.Live when EditorApplication.isPaused => ("PAUSED", Caution),
                RuntimeState.Live => ("LIVE", Good),
                RuntimeState.Deploying => ("DEPLOYING", Caution),
                RuntimeState.Waiting => ("WAITING FOR CORE", InkDim),
                _ => ("OFFLINE", InkFaint),
            };

            float width = Theme.Measure(label, chipStyle).x + 24f;
            var chip = new Rect(rect.x + 124f, rect.y + 14f, width, 18f);

            Theme.FillRounded(chip, Theme.WithAlpha(color, 0.12f), 9f);
            Theme.FillRounded(Theme.Circle(new Vector2(chip.x + 9f, chip.center.y), 3f), color, 3f);
            Theme.Draw(new Rect(chip.x + 16f, chip.y, width - 16f, chip.height), label, chipStyle, color);

            if (state == RuntimeState.Offline)
                Theme.Draw(new Rect(rect.x + 18f, rect.y + 36f, rect.width - 36f, 16f), "Enter Play Mode to see the runtime live.", legendStyle, InkFaint);
        }

        private void DrawLegend(Rect rect)
        {
            float x = rect.x + 18f;
            float y = rect.yMax - 24f;

            Theme.FillRounded(Theme.Circle(new Vector2(x + 5f, y + 7f), 5f), Ink, 5f);
            Theme.Draw(new Rect(x + 16f, y, 120f, 14f), "Woven subsystem", legendStyle, InkFaint);

            x += 124f;
            Theme.StrokeRounded(Theme.Circle(new Vector2(x + 5f, y + 7f), 5f), Ink, 5f, 1.3f);
            Theme.Draw(new Rect(x + 16f, y, 100f, 14f), "Static service", legendStyle, InkFaint);

            x += 108f;
            Theme.FillRounded(Theme.Circle(new Vector2(x + 5f, y + 7f), 5f), IdleFill, 5f);
            Theme.StrokeRounded(Theme.Circle(new Vector2(x + 5f, y + 7f), 5f), IdleStroke, 5f, 1f);
            Theme.Draw(new Rect(x + 16f, y, 100f, 14f), "Not running", legendStyle, InkFaint);
        }
        #endregion

        #region Details panel:
        private void DrawPanel(Rect rect)
        {
            Theme.Fill(rect, PanelBackground);
            Theme.Fill(new Rect(rect.x, rect.y, 1f, rect.height), Rule);

            var node = selectedKey == CoreKey ? coreNode : nodes.Find(candidate => candidate.Key == selectedKey);

            if (node == null)
            {
                selectedKey = CoreKey;
                node = coreNode;
            }

            GUILayout.BeginArea(rect);
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginHorizontal();
            GUILayout.Space(PanelPadding);
            GUILayout.BeginVertical(GUILayout.Width(PanelWidth - PanelPadding * 2f - 4f));
            GUILayout.Space(16f);

            DrawPanelHeader(node);

            try
            {
                DrawDetails(node);
            }
            catch (Exception exception)
            {
                // A subsystem mid-deployment or mid-shutdown can be in any state; never break the window over it.
                Section("Unavailable");
                Note(exception.GetType().Name + ": " + exception.Message, Bad);
            }

            GUILayout.Space(18f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPanelHeader(Node node)
        {
            var title = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            Theme.Draw(title, Theme.Fit(node.Label, titleStyle, title.width), titleStyle, Ink);

            string subtitle = node.Kind switch
            {
                NodeKind.Core => "Core  ·  " + Kind(typeof(Threadlink)),
                NodeKind.Service => "Static service",
                _ => (node.Type != null && Array.IndexOf(NativeWoven, node.Type) >= 0 ? "Native subsystem" : "User subsystem") + "  ·  " + Kind(node.Type),
            };

            var sub = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            Theme.Draw(sub, Theme.Fit(subtitle, subtitleStyle, sub.width), subtitleStyle, InkDim);
            GUILayout.Space(4f);
        }

        private void DrawDetails(Node node)
        {
            if (node.Kind == NodeKind.Core)
            {
                DrawCoreDetails();
                return;
            }

            if (node.Kind == NodeKind.Service)
            {
                switch (node.Key)
                {
                    case IrisKey: DrawIrisDetails(); break;
                    case NexusKey: DrawNexusDetails(); break;
                    case InitiumKey: DrawInitiumDetails(); break;
                    case ScribeKey: DrawScribeDetails(); break;
                }

                return;
            }

            DrawSubsystemDetails(node);

            if (!node.Live)
                return;

            switch (node.Subsystem)
            {
                case Sentinel sentinel: DrawSentinelDetails(sentinel); break;
                case Chronos _: DrawChronosDetails(); break;
                case Dextra dextra: DrawDextraDetails(dextra); break;
                case Aura _:
                    Section("Audio");
                    Row("Listener volume", AudioListener.volume.ToString("0.00"));
                    break;
            }

            if (node.Subsystem is IEditorOnlyRegister register)
                DrawRegister(register);
        }

        private void DrawCoreDetails()
        {
            Section("Deployment");
            Row("State", StateLabel(), StateColor());

            if (core == null)
            {
                Note(state == RuntimeState.Offline
                    ? "Threadlink deploys once the first scene has loaded in Play Mode. Click any node to see what it does and, once running, its live state."
                    : "Waiting for the core to deploy.");
            }
            else
            {
                Row("Woven subsystems", weaveOrder.Count.ToString());

                if (Chronos.TryGetSingleton(out _))
                    Row("Since deployment", FormatTime(Chronos.CurrentTimeSinceDeployment));

                ObjectRow("Native config", core.NativeConfig);
                ObjectRow("User config", core.UserConfig);

                Section("Weave order");

                for (int i = 0; i < weaveOrder.Count; i++)
                {
                    var subsystem = weaveOrder[i];
                    bool linked = IsLinked(subsystem);
                    Row((i + 1) + ".  " + subsystem.GetType().Name, linked ? Kind(subsystem.GetType()) : "unlinked", linked ? InkDim : Caution);
                }
            }

            DrawBudgetDetails();
            DrawMemoryDetails();
            DrawRenderingDetails();
        }

        private void DrawBudgetDetails()
        {
            Section("Frame budget");

            var popup = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            Theme.Draw(new Rect(popup.x, popup.y, popup.width * 0.5f, popup.height), "Target", keyStyle, InkDim);

            var options = new string[TargetFrameRates.Length];
            options[0] = "Auto (" + ResolveTargetFrameRate(0) + " fps)";

            for (int i = 1; i < options.Length; i++)
                options[i] = TargetFrameRates[i] + " fps";

            targetFrameRateIndex = EditorGUI.Popup(new Rect(popup.xMax - 120f, popup.y + 1f, 120f, 18f), targetFrameRateIndex, options);

            float budget = BudgetMs();
            Row("Budget", budget.ToString("0.0") + " ms");

            if (!EditorApplication.isPlaying)
            {
                Note("Frame metrics are sampled in Play Mode.");
                return;
            }

            float usage = budget > 0f ? mainThreadMs / budget : 0f;
            Row("Main thread", mainThreadMs.ToString("0.0") + " ms  ·  " + Mathf.RoundToInt(usage * 100f) + "%", UsageColor(usage));
            Bar(usage, UsageColor(usage));
            Row("Headroom", (budget - mainThreadMs).ToString("0.0") + " ms", budget - mainThreadMs < 0f ? Bad : InkDim);
            Row("Frame rate", fps.ToString("0") + " fps");

            GUILayout.Space(6f);
            Sparkline(budget);
            Note("Main thread time over the last 12 seconds, against the budget line. In the Editor it includes Editor work.");
        }

        private void DrawMemoryDetails()
        {
            Section("Memory");

            if (!EditorApplication.isPlaying)
            {
                Note("Memory is sampled in Play Mode.");
                return;
            }

            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long deviceMemory = SystemInfo.systemMemorySize * 1024L * 1024L;
            long systemUsed = systemMemory.Valid ? systemMemory.LastValue : -1;

            Row("Allocated", Theme.Bytes(Profiler.GetTotalAllocatedMemoryLong()));
            Row("Reserved", Theme.Bytes(Profiler.GetTotalReservedMemoryLong()));
            Row("Graphics driver", Theme.Bytes(Profiler.GetAllocatedMemoryForGraphicsDriver()));
            Row("Managed heap", Theme.Bytes(monoUsed) + " of " + Theme.Bytes(monoHeap));
            Bar(monoHeap > 0 ? (float)monoUsed / monoHeap : 0f, InkDim);

            if (systemUsed > 0 && deviceMemory > 0)
            {
                float share = (float)systemUsed / deviceMemory;
                Row("Process", Theme.Bytes(systemUsed) + "  ·  " + Mathf.RoundToInt(share * 100f) + "% of RAM", UsageColor(share * 1.4f));
                Bar(share, UsageColor(share * 1.4f));
            }

            Row("GC allocation", Theme.Bytes((long)gcPerFrame) + " / frame");
            Row("GC rate", Theme.Bytes((long)(gcPerFrame * fps)) + " / s");
            Row("GC collections", (GC.CollectionCount(0) - gcBaseline) + " this session");
            Note("Editor figures include the Editor itself. Profile a development build for numbers to budget against.");
        }

        private void DrawRenderingDetails()
        {
            if (!EditorApplication.isPlaying)
                return;

            Section("Rendering");
            Row("Batches", RecorderValue(batches));
            Row("Draw calls", RecorderValue(drawCalls));
            Row("Set-pass calls", RecorderValue(setPassCalls));
            Row("Triangles", RecorderValue(triangles));
        }

        private void DrawSubsystemDetails(Node node)
        {
            var type = node.Type;

            Section("Subsystem");
            Row("Status", node.Live ? "Woven and linked" : EditorApplication.isPlaying ? "Not woven" : "Not running", node.Live ? Good : InkDim);

            if (type == null)
                return;

            Row("Type", type.FullName);
            Row("Assembly", type.Assembly.GetName().Name);
            Row("ID", "0x" + (node.Subsystem?.ID ?? HashFunctions.ToXxHash32(type.FullName)).ToString("X8"));

            var lifecycle = new List<string>(4);
            if (typeof(IAddressablesPreloader).IsAssignableFrom(type)) lifecycle.Add("Preloads");
            lifecycle.Add("Boots");
            if (typeof(IInitializable).IsAssignableFrom(type)) lifecycle.Add("Initialises");
            if (typeof(IDisposable).IsAssignableFrom(type)) lifecycle.Add("Disposes");

            Row("Lifecycle", string.Join(" · ", lifecycle));

            if (!node.Live)
                Note(Describe(type));
        }

        private void DrawRegister(IEditorOnlyRegister register)
        {
            registryBuffer.Clear();
            registryBuffer.AddRange(register.EditorOnly_Registry);

            Section("Linked " + register.EditorOnly_ObjectType.Name + "  (" + registryBuffer.Count + ")");

            if (registryBuffer.Count == 0)
            {
                Note("Nothing linked.");
                return;
            }

            int shown = Mathf.Min(registryBuffer.Count, 200);

            for (int i = 0; i < shown; i++)
            {
                var value = registryBuffer[i].Value;
                string name = value is INamable namable ? namable.Name : value?.GetType().Name ?? "null";

                if (value is UnityObject unityObject)
                    ObjectRow(name, unityObject, "0x" + registryBuffer[i].Key.ToString("X8"));
                else
                    Row(name, "0x" + registryBuffer[i].Key.ToString("X8"));
            }

            if (registryBuffer.Count > shown)
                Note("And " + (registryBuffer.Count - shown) + " more.");
        }

        private void DrawSentinelDetails(Sentinel sentinel)
        {
            var result = sentinel.InitializationResult;

            Section("Platform");
            Row("State", sentinel.State.ToString(), sentinel.State switch
            {
                Sentinel.DeploymentState.Ready => Good,
                Sentinel.DeploymentState.Failed => Bad,
                _ => Caution,
            });
            Row("Platform", sentinel.ActivePlatform.ToString());
            Row("Distribution", sentinel.ActiveDistribution.ToString());
            Row("Module", string.IsNullOrEmpty(sentinel.ActiveModuleDisplayName) ? "—" : sentinel.ActiveModuleDisplayName);
            Row("Result", result.Succeeded ? "Success" : result.Error.ToString(), result.Succeeded ? InkDim : Bad);

            if (!result.Succeeded && !string.IsNullOrEmpty(result.Message))
                Note(result.Message, Bad);

            Section("Capabilities");
            var capabilities = sentinel.Capabilities;
            Note(capabilities == SentinelCapability.None ? "None" : capabilities.ToString().Replace(", ", "  ·  "), InkDim);
        }

        private void DrawChronosDetails()
        {
            Section("Time");
            Row("Frame rate", Chronos.DeltaTime > 0f ? (1f / Chronos.DeltaTime).ToString("0") + " fps" : "—");
            Row("Delta time", (Chronos.DeltaTime * 1000f).ToString("0.00") + " ms");
            Row("Smooth delta", (Chronos.SmoothDeltaTime * 1000f).ToString("0.00") + " ms");
            Row("Unscaled delta", (Chronos.UnscaledDeltaTime * 1000f).ToString("0.00") + " ms");
            Row("Fixed delta", (Chronos.FixedDeltaTime * 1000f).ToString("0.00") + " ms");
            Row("Time scale", Chronos.TimeScale.ToString("0.##"), Chronos.TimeScale <= 0f ? Caution : InkDim);

            Section("Playtime");
            Row("Total", FormatTime(Chronos.TotalPlaytime));
            Row("Counting", Chronos.CountTotalPlaytime ? Chronos.PlaytimeCountingMode.ToString() : "Paused");
            Row("Since deployment", FormatTime(Chronos.CurrentTimeSinceDeployment));
        }

        private void DrawDextraDetails(Dextra dextra)
        {
            Section("Input");
            Row("Device", dextra.CurrentInputDevice.ToString());
            ObjectRow("Pointed object", dextra.CurrentPointedObject);

            Section("UI stack");
            int index = 0;

            foreach (var userInterface in dextra.EditorOnly_StackedInterfaces)
                Row(index++ == 0 ? "Top" : index.ToString(), userInterface.Name);

            if (index == 0)
                Note("No interfaces stacked.");
        }

        private void DrawIrisDetails()
        {
            Note("Iris is the event bus: subscribe, unsubscribe and publish by generated event ID.");

            if (!EditorApplication.isPlaying)
                return;

            irisEvents ??= (ThreadlinkIDs.Iris.Events[])Enum.GetValues(typeof(ThreadlinkIDs.Iris.Events));

            Section("Events");
            Row("Declared", irisEvents.Length.ToString());
            Row("With listeners", irisListeners.Count.ToString());
            Row("Listeners", irisTotal.ToString());

            if (irisListeners.Count == 0)
                return;

            Section("Listeners by event");

            foreach (var (eventName, count) in irisListeners)
                Row(eventName, count.ToString());
        }

        private void DrawNexusDetails()
        {
            Note("Nexus loads, holds and presents scenes. A scene stays resident while anything holds it.");

            if (!EditorApplication.isPlaying)
                return;

            Section("Presentation");
            Row("Presented", Nexus.Presented != null ? Nexus.Presented.ScenePointer.ToString() : "None");
            Row("Active scene", SceneManager.GetActiveScene().name);

            Section("Scenes");
            int count = 0;

            foreach (var (pointer, sceneState, holds, _) in Nexus.EditorOnly_Scenes)
            {
                count++;
                Row(pointer, sceneState + "  ·  " + holds + (holds == 1 ? " hold" : " holds"), sceneState == "Resident" ? InkDim : Caution);
            }

            if (count == 0)
                Note("Nexus holds no scenes.");

            Section("Loaded Unity scenes");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Row(scene.name, scene == SceneManager.GetActiveScene() ? "active" : scene.isLoaded ? "loaded" : "loading");
            }
        }

        private void DrawInitiumDetails()
        {
            Note("Initium is the initialisation pipeline: it preloads, boots and initialises subsystems at deployment, then the " +
                "IDiscoverable objects of loaded scenes and of every scene Nexus holds, and discards them before Nexus unloads a scene.");

            if (!EditorApplication.isPlaying)
                return;

            // Rescan at most once a second, and only on Layout, so Layout and Repaint lay out the same rows.
            if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup - lastDiscoverableScan > 1d)
            {
                lastDiscoverableScan = EditorApplication.timeSinceStartup;
                discoverables.Clear();

                var counts = new Dictionary<string, int>(8);

                foreach (var behaviour in UnityObject.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Include))
                {
                    if (behaviour is not IDiscoverable)
                        continue;

                    string scene = behaviour.gameObject.scene.name;
                    counts[scene] = counts.TryGetValue(scene, out int existing) ? existing + 1 : 1;
                }

                foreach (var pair in counts)
                    discoverables.Add((pair.Key, pair.Value));

                discoverables.Sort((a, b) => b.Count.CompareTo(a.Count));
            }

            Section("Discoverable objects");

            foreach (var (scene, count) in discoverables)
                Row(string.IsNullOrEmpty(scene) ? "(no scene)" : scene, count.ToString());

            if (discoverables.Count == 0)
                Note("No discoverable objects are loaded.");
        }

        private void DrawScribeDetails()
        {
            Note("Scribe formats and sends Threadlink's log messages to the Unity console.");

            if (!EditorApplication.isPlaying)
                return;

            // Messages arrive on any thread: take the snapshot on Layout, so Layout and Repaint lay out the same rows.
            if (Event.current.type == EventType.Layout || scribeSnapshot == null)
            {
                lock (logLock)
                {
                    scribeCounts = (logCount, warningCount, errorCount);
                    scribeSnapshot = recentMessages.ToArray();
                }
            }

            var (logs, warnings, errors) = scribeCounts;
            var recent = scribeSnapshot;

            Section("Console this session");
            Row("Messages", logs.ToString());
            Row("Warnings", warnings.ToString(), warnings > 0 ? Caution : InkDim);
            Row("Errors", errors.ToString(), errors > 0 ? Bad : InkDim);

            Section("Recent Threadlink messages");

            if (recent.Length == 0)
            {
                Note("None yet.");
                return;
            }

            for (int i = recent.Length - 1; i >= 0; i--)
            {
                var color = recent[i].Type switch
                {
                    LogType.Warning => Caution,
                    LogType.Log => InkDim,
                    _ => Bad,
                };

                Note(recent[i].Text, color);
                GUILayout.Space(2f);
            }
        }
        #endregion

        #region Metrics strip:
        private void DrawStrip(Rect rect)
        {
            Theme.Fill(rect, StripBackground);
            Theme.Fill(new Rect(rect.x, rect.y, rect.width, 1f), Rule);

            bool playing = EditorApplication.isPlaying;
            float budget = BudgetMs();
            float usage = budget > 0f ? mainThreadMs / budget : 0f;
            long monoUsed = playing ? Profiler.GetMonoUsedSizeLong() : -1;
            long monoHeap = playing ? Profiler.GetMonoHeapSizeLong() : -1;

            const int cells = 6;
            float width = rect.width / cells;

            for (int i = 1; i < cells; i++)
                Theme.Fill(new Rect(rect.x + width * i, rect.y + 12f, 1f, rect.height - 24f), Rule);

            Metric(Cell(rect, 0, width), "FPS", playing ? fps.ToString("0") : "—", "target " + ResolveTargetFrameRate(TargetFrameRates[targetFrameRateIndex]), Ink);

            var frame = Cell(rect, 1, width);
            Metric(frame, "MAIN THREAD", playing ? mainThreadMs.ToString("0.0") + " ms" : "—",
                playing ? Mathf.RoundToInt(usage * 100f) + "% of " + budget.ToString("0.0") + " ms" : "budget " + budget.ToString("0.0") + " ms",
                playing ? UsageColor(usage) : Ink);

            if (playing && Event.current.type == EventType.Repaint)
            {
                var bar = new Rect(frame.x, frame.yMax - 3f, frame.width - 16f, 3f);
                Theme.FillRounded(bar, Theme.WithAlpha(Ink, 0.08f), 1.5f);
                Theme.FillRounded(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(usage), bar.height), UsageColor(usage), 1.5f);
            }

            Metric(Cell(rect, 2, width), "GC ALLOC", playing ? Theme.Bytes((long)gcPerFrame) : "—",
                playing ? "per frame  ·  " + (GC.CollectionCount(0) - gcBaseline) + " collections" : "per frame", Ink);

            Metric(Cell(rect, 3, width), "MANAGED HEAP", playing ? Theme.Bytes(monoUsed) : "—", playing ? "of " + Theme.Bytes(monoHeap) : "Play Mode only", Ink);

            Metric(Cell(rect, 4, width), "MEMORY", playing ? Theme.Bytes(Profiler.GetTotalAllocatedMemoryLong()) : "—",
                playing ? Theme.Bytes(Profiler.GetTotalReservedMemoryLong()) + " reserved" : "allocated", Ink);

            Metric(Cell(rect, 5, width), "BATCHES", playing ? RecorderValue(batches) : "—",
                playing ? RecorderValue(drawCalls) + " draw calls" : "rendering", Ink);
        }

        private static Rect Cell(Rect strip, int index, float width) => new(strip.x + width * index + 18f, strip.y + 10f, width - 24f, strip.height - 18f);

        private void Metric(Rect rect, string label, string value, string note, Color valueColor)
        {
            Theme.Draw(new Rect(rect.x, rect.y, rect.width, 12f), label, metricLabelStyle, InkFaint);
            Theme.Draw(new Rect(rect.x, rect.y + 12f, rect.width, 20f), Theme.Fit(value, metricValueStyle, rect.width), metricValueStyle, valueColor);
            Theme.Draw(new Rect(rect.x, rect.y + 32f, rect.width, 12f), Theme.Fit(note, metricNoteStyle, rect.width), metricNoteStyle, InkFaint);
        }
        #endregion

        #region Panel primitives:
        private void Section(string title)
        {
            GUILayout.Space(16f);
            var rect = GUILayoutUtility.GetRect(0f, 14f, GUILayout.ExpandWidth(true));
            Theme.Draw(rect, title.ToUpperInvariant(), sectionStyle, InkFaint);
            Theme.Fill(new Rect(rect.x, rect.yMax + 3f, rect.width, 1f), Rule);
            GUILayout.Space(7f);
        }

        private void Row(string key, string value) => Row(key, value, InkDim);

        private void Row(string key, string value, Color valueColor)
        {
            var rect = GUILayoutUtility.GetRect(0f, 19f, GUILayout.ExpandWidth(true));

            if (Event.current.type != EventType.Repaint)
                return;

            float valueWidth = Mathf.Min(Theme.Measure(value, valueStyle).x, rect.width * 0.62f);
            float keyWidth = rect.width - valueWidth - 10f;

            Theme.Draw(new Rect(rect.x, rect.y, keyWidth, rect.height), Theme.Fit(key, keyStyle, keyWidth), keyStyle, Ink);
            Theme.Draw(new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height), Theme.Fit(value, valueStyle, valueWidth), valueStyle, valueColor);
        }

        /// <summary>A row whose value pings <paramref name="target"/> when clicked.</summary>
        private void ObjectRow(string key, UnityObject target, string value = null)
        {
            var rect = GUILayoutUtility.GetRect(0f, 19f, GUILayout.ExpandWidth(true));
            value ??= target != null ? target.name : "None";

            float valueWidth = Mathf.Min(Theme.Measure(value, valueStyle).x, rect.width * 0.62f);
            float keyWidth = rect.width - valueWidth - 10f;
            var valueRect = new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            bool clickable = target != null;

            if (clickable)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    EditorGUIUtility.PingObject(target);
                    Event.current.Use();
                }
            }

            bool hovered = clickable && rect.Contains(Event.current.mousePosition);

            Theme.Draw(new Rect(rect.x, rect.y, keyWidth, rect.height), Theme.Fit(key, keyStyle, keyWidth), keyStyle, Ink);
            Theme.Draw(valueRect, Theme.Fit(value, valueStyle, valueWidth), valueStyle, clickable ? (hovered ? Ink : Theme.WithAlpha(Ink, 0.75f)) : InkFaint);

            if (clickable)
                Theme.Fill(new Rect(valueRect.x, valueRect.yMax - 3f, valueRect.width, 1f), Theme.WithAlpha(Ink, hovered ? 0.6f : 0.18f));
        }

        private void Note(string text) => Note(text, InkFaint);

        private void Note(string text, Color color)
        {
            float width = PanelWidth - PanelPadding * 2f - 4f;
            var content = new GUIContent(text);
            var rect = GUILayoutUtility.GetRect(content, noteStyle, GUILayout.Width(width), GUILayout.Height(noteStyle.CalcHeight(content, width) + 2f));

            Theme.Draw(rect, text, noteStyle, color);
        }

        private void Bar(float fraction, Color color)
        {
            GUILayout.Space(2f);
            var rect = GUILayoutUtility.GetRect(0f, 4f, GUILayout.ExpandWidth(true));
            Theme.FillRounded(rect, Theme.WithAlpha(Ink, 0.08f), 2f);
            Theme.FillRounded(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color, 2f);
            GUILayout.Space(4f);
        }

        private void Sparkline(float budget)
        {
            var rect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));

            if (Event.current.type != EventType.Repaint)
                return;

            Theme.FillRounded(rect, Theme.WithAlpha(Ink, 0.03f), 4f);

            float peak = budget * 1.25f;

            for (int i = 0; i < historyCount; i++)
                peak = Mathf.Max(peak, history[i] * 1.1f);

            float budgetY = rect.yMax - 4f - (budget / peak) * (rect.height - 8f);
            Theme.Fill(new Rect(rect.x + 4f, budgetY, rect.width - 8f, 1f), Theme.WithAlpha(Caution, 0.45f));

            if (historyCount < 2)
                return;

            float step = (rect.width - 8f) / (HistoryLength - 1);

            for (int i = 0; i < historyCount; i++)
            {
                float x = rect.x + 4f + (HistoryLength - historyCount + i) * step;
                float y = rect.yMax - 4f - Mathf.Clamp01(history[i] / peak) * (rect.height - 8f);
                spark[i] = new Vector3(x, y, 0f);
            }

            Handles.color = Ink;
            Handles.DrawAAPolyLine(1.5f, historyCount, spark);
            Handles.color = Color.white;
        }
        #endregion

        #region Helpers:
        private int ResolveTargetFrameRate(int selected)
        {
            if (selected > 0)
                return selected;

            if (Application.targetFrameRate > 0)
                return Application.targetFrameRate;

            int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            return refresh > 0 ? refresh : 60;
        }

        private float BudgetMs()
        {
            targetFrameRateIndex = Mathf.Clamp(targetFrameRateIndex, 0, TargetFrameRates.Length - 1);
            return 1000f / ResolveTargetFrameRate(TargetFrameRates[targetFrameRateIndex]);
        }

        private static Color UsageColor(float usage) => usage >= 1f ? Bad : usage >= 0.75f ? Caution : Good;

        private static string RecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.LastValue > 0 ? recorder.LastValue.ToString("N0") : "—";
        }

        private string StateLabel() => state switch
        {
            RuntimeState.Live => "Deployed",
            RuntimeState.Deploying => "Deploying",
            RuntimeState.Waiting => "Waiting for the core",
            _ => "Offline",
        };

        private Color StateColor() => state switch
        {
            RuntimeState.Live => Good,
            RuntimeState.Deploying => Caution,
            _ => InkDim,
        };

        private static string FormatTime(float seconds)
        {
            var span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1d ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
        }

        /// <summary>Weaver, Linker, Register or Subsystem: the most specific base <paramref name="type"/> derives from.</summary>
        private static string Kind(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;

                var definition = current.GetGenericTypeDefinition();

                if (definition == typeof(Weaver<,>)) return "Weaver of " + current.GetGenericArguments()[1].Name;
                if (definition == typeof(Linker<,>)) return "Linker of " + current.GetGenericArguments()[1].Name;
                if (definition == typeof(Register<,>)) return "Register of " + current.GetGenericArguments()[1].Name;
            }

            return "Subsystem";
        }

        private static string Describe(Type type)
        {
            if (type == typeof(Sentinel)) return "Sentinel resolves the platform and distribution, and exposes platform services: accounts, saves, achievements, presence and invites.";
            if (type == typeof(Chronos)) return "Chronos tracks time: delta times, time scale, pausing and total playtime.";
            if (type == typeof(Dextra)) return "Dextra handles input: the current device, input maps, and the stack of user interfaces.";
            if (type == typeof(Aura)) return "Aura mixes audio: music and ambience channels, UI effects, and spatial audio zones in the presented scene.";

            return "A user subsystem declared in the project. It appears here once woven at deployment.";
        }
        #endregion
    }
}
