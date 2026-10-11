namespace Threadlink.Editor
{
    // Types whose names match a namespace are always referenced through an alias or global::, never a namespace import:
    // inside Threadlink.Editor, a bare name such as Sentinel resolves to the Threadlink.Editor.Sentinel namespace first.
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Profiling;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Profiling;
    using DebugType = global::Threadlink.Core.NativeSubsystems.Scribe.DebugType;
    using HashFunctions = global::Threadlink.Shared.HashFunctions;
    using IAddressablesPreloader = global::Threadlink.Shared.IAddressablesPreloader;
    using IInitializable = global::Threadlink.Shared.IInitializable;
    using IMonitorable = global::Threadlink.Shared.IMonitorable;
    using IThreadlinkSubsystem = global::Threadlink.Shared.IThreadlinkSubsystem;
    using MonitoredAttribute = global::Threadlink.Shared.MonitoredAttribute;
    using MonitorLineKind = global::Threadlink.Shared.MonitorLineKind;
    using MonitorRegistry = global::Threadlink.Shared.MonitorRegistry;
    using MonitorReport = global::Threadlink.Shared.MonitorReport;
    using MonitorTone = global::Threadlink.Shared.MonitorTone;
    using ScribeService = global::Threadlink.Core.NativeSubsystems.Scribe.Scribe;
    using Theme = ThreadlinkEditorTheme;
    using ThreadlinkCore = global::Threadlink.Core.Threadlink;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// A live view of Threadlink's runtime: the core, every woven subsystem and every monitored service as a radial
    /// graph, the selected node's report, and frame, memory and rendering metrics against a frame budget.
    /// <para></para>
    /// The window knows no subsystem or service by name. Nodes come from the core's woven subsystems, from static
    /// methods marked <see cref="MonitoredAttribute"/>, and from <see cref="MonitorRegistry"/>; their content comes from
    /// <see cref="IMonitorable.Report"/>.
    /// </summary>
    internal sealed class ThreadlinkRuntimeMonitorWindow : EditorWindow
    {
        #region Model:
        private enum NodeKind : byte { Core, Subsystem, Service }
        private enum RuntimeState : byte { Offline, Waiting, Deploying, Live }

        private sealed class Node
        {
            public string Key;
            public string Label;
            public string Subtitle;
            public string Description;
            public NodeKind Kind;
            public Type Type;
            public IMonitorable Source;
            public Action<MonitorReport> StaticReport;
            public bool Live;
            public bool Alert;
            public string Caption;
            public Vector2 Centre;
            public float Radius;
            public int Index;

            public bool CanReport => Live && (Source != null || StaticReport != null);
        }

        /// <summary>A static service: a static <c>Report(MonitorReport)</c> method marked <see cref="MonitoredAttribute"/>.</summary>
        private sealed class ServiceInfo
        {
            public string Key;
            public string Name;
            public string Description;
            public int Order;
            public Type DeclaringType;
            public Action<MonitorReport> Report;
        }

        private const string CoreKey = "core";
        private const double ModelInterval = 0.1d;
        private const double DetailInterval = 0.25d;
        private static readonly int[] TargetFrameRates = { 0, 30, 60, 90, 120, 144, 240 };

        private static List<ServiceInfo> services;
        private static List<Type> declaredSubsystems;
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
        private ThreadlinkCore core;
        private RuntimeState state;
        private bool wasPlaying;

        private readonly MonitorReport summaryReport = new();
        private readonly MonitorReport detailReport = new();
        private string detailKey;
        private string detailError;
        private double lastModelRefresh = -1d, lastDetailRefresh = -1d;

        private double lastSample, lastRepaint;
        private string hoveredKey, pendingKey;

        // Frame and memory metrics, sampled ten times a second in Play Mode.
        private ProfilerRecorder mainThread, gcAllocInFrame, drawCalls, batches, setPassCalls, triangles, systemMemory;
        private float fps, mainThreadMs, gcPerFrame;
        private int lastFrameCount, gcBaseline;
        private double lastFrameTime;
        private readonly float[] history = new float[HistoryLength];
        private int historyCount;

        private readonly Vector3[] segment = new Vector3[2];
        private readonly Vector3[] arrow = new Vector3[3];
        private readonly Vector3[] spark = new Vector3[HistoryLength];

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

            gcBaseline = GC.CollectionCount(0);

            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

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
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                fps = mainThreadMs = gcPerFrame = 0f;
                historyCount = 0;
                lastFrameTime = 0d;
                gcBaseline = GC.CollectionCount(0);
            }

            lastModelRefresh = lastDetailRefresh = -1d;
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
        #endregion

        #region Discovery:
        /// <summary>Every static method marked <see cref="MonitoredAttribute"/>, in order, then by name.</summary>
        private static List<ServiceInfo> Services()
        {
            if (services != null)
                return services;

            services = new List<ServiceInfo>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<MonitoredAttribute>())
            {
                var attribute = method.GetCustomAttribute<MonitoredAttribute>(false);
                var parameters = method.GetParameters();
                var declaringType = method.DeclaringType;

                if (!method.IsStatic || method.ReturnType != typeof(void) || parameters.Length != 1
                || parameters[0].ParameterType != typeof(MonitorReport) || declaringType == null || method.ContainsGenericParameters)
                {
                    ScribeService.ToUnityConsole(ScribeService.Send<ThreadlinkRuntimeMonitorWindow>(
                        declaringType?.FullName, ".", method.Name,
                        " is marked [Monitored] but is not a static void Report(MonitorReport) method, so it is ignored."), DebugType.Warning);
                    continue;
                }

                string name = string.IsNullOrEmpty(attribute.Name) ? declaringType.Name : attribute.Name;

                services.Add(new ServiceInfo
                {
                    Key = "service:" + declaringType.FullName + "." + method.Name,
                    Name = name,
                    Description = attribute.Description,
                    Order = attribute.Order,
                    DeclaringType = declaringType,
                    Report = (Action<MonitorReport>)Delegate.CreateDelegate(typeof(Action<MonitorReport>), method),
                });
            }

            services.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Name, b.Name));
            return services;
        }

        /// <summary>Every concrete subsystem type in the project, shown before deployment in attribute order, then by name.</summary>
        private static List<Type> DeclaredSubsystems()
        {
            if (declaredSubsystems != null)
                return declaredSubsystems;

            declaredSubsystems = new List<Type>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<IThreadlinkSubsystem>())
            {
                if (!type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters && type != typeof(ThreadlinkCore))
                    declaredSubsystems.Add(type);
            }

            declaredSubsystems.Sort((a, b) =>
            {
                int orderA = OrderOf(a), orderB = OrderOf(b);
                return orderA != orderB ? orderA.CompareTo(orderB) : string.CompareOrdinal(a.Name, b.Name);
            });

            return declaredSubsystems;
        }

        private static MonitoredAttribute AttributeOf(Type type) => type?.GetCustomAttribute<MonitoredAttribute>(false);

        private static int OrderOf(Type type) => AttributeOf(type)?.Order ?? 0;

        private static bool IsFrameworkType(Type type)
        {
            return type != null && (type.Assembly == typeof(ThreadlinkCore).Assembly || type.Assembly == typeof(MonitorReport).Assembly);
        }
        #endregion

        #region Graph model:
        private void RefreshModel()
        {
            core = null;

            if (!EditorApplication.isPlaying)
                state = RuntimeState.Offline;
            else if (!ThreadlinkCore.TryGetSingleton(out core))
                state = RuntimeState.Waiting;
            else
                state = core.IsDeployed ? RuntimeState.Live : RuntimeState.Deploying;

            nodes.Clear();

            coreNode = GetNode(CoreKey, "Threadlink", NodeKind.Core);
            coreNode.Type = typeof(ThreadlinkCore);
            coreNode.Source = core;
            coreNode.StaticReport = null;
            coreNode.Live = core != null;
            coreNode.Subtitle = "Core  ·  " + Kind(typeof(ThreadlinkCore));
            coreNode.Description = "Threadlink deploys once the first scene has loaded in Play Mode. Click any node to see what it does and, once running, its live state.";

            AddSubsystemNodes();
            AddServiceNodes();
            AddRegisteredNodes();

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Index = i;
                Summarise(nodes[i]);
            }

            Summarise(coreNode);
        }

        private void AddSubsystemNodes()
        {
            bool woven = false;

            if (core != null)
            {
                foreach (var subsystem in core.WovenSubsystems)
                {
                    if (subsystem == null)
                        continue;

                    var node = SubsystemNode(subsystem.GetType());
                    node.Source = subsystem as IMonitorable;
                    node.Live = IsLinked(subsystem);
                    nodes.Add(node);
                    woven = true;
                }
            }

            // Before deployment, every subsystem declared in the project.
            if (!woven)
            {
                foreach (var type in DeclaredSubsystems())
                {
                    var node = SubsystemNode(type);
                    node.Source = null;
                    node.Live = false;
                    nodes.Add(node);
                }
            }
        }

        private Node SubsystemNode(Type type)
        {
            var node = GetNode("subsystem:" + type.FullName, type.Name, NodeKind.Subsystem);

            node.Type = type;
            node.StaticReport = null;
            node.Subtitle = (IsFrameworkType(type) ? "Native subsystem" : "User subsystem") + "  ·  " + Kind(type);
            node.Description = AttributeOf(type)?.Description;
            return node;
        }

        private void AddServiceNodes()
        {
            foreach (var service in Services())
            {
                var node = GetNode(service.Key, service.Name, NodeKind.Service);

                node.Type = service.DeclaringType;
                node.Source = null;
                node.StaticReport = service.Report;
                node.Live = EditorApplication.isPlaying;
                node.Subtitle = (IsFrameworkType(service.DeclaringType) ? "Native service" : "User service") + "  ·  " + service.DeclaringType.Name;
                node.Description = service.Description;
                nodes.Add(node);
            }
        }

        private void AddRegisteredNodes()
        {
            if (!EditorApplication.isPlaying)
                return;

            foreach (var entry in MonitorRegistry.Entries)
            {
                if (entry.Source == null || (entry.Source is UnityObject unityObject && unityObject == null))
                    continue;

                var type = entry.Source.GetType();
                var node = GetNode("registered:" + entry.Name + "#" + type.FullName, entry.Name, NodeKind.Service);

                node.Type = type;
                node.Source = entry.Source;
                node.StaticReport = null;
                node.Live = true;
                node.Subtitle = "Registered service  ·  " + type.Name;
                node.Description = AttributeOf(type)?.Description;
                nodes.Add(node);
            }
        }

        private Node GetNode(string key, string label, NodeKind kind)
        {
            if (!nodeCache.TryGetValue(key, out var node))
            {
                node = new Node { Key = key };
                nodeCache[key] = node;
            }

            node.Label = label;
            node.Kind = kind;
            return node;
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

        /// <summary>Asks <paramref name="node"/> for its summary: the caption and alert shown on the graph.</summary>
        private void Summarise(Node node)
        {
            node.Caption = string.Empty;
            node.Alert = false;

            if (!node.CanReport)
            {
                node.Caption = node.Kind == NodeKind.Core ? StateCaption() : state == RuntimeState.Live && node.Kind == NodeKind.Subsystem ? "not woven" : string.Empty;
                return;
            }

            if (TryReport(node, summaryReport, false, out _))
            {
                node.Caption = summaryReport.Caption ?? string.Empty;
                node.Alert = summaryReport.Alert;
            }
            else
            {
                node.Caption = "report failed";
                node.Alert = true;
            }

            if (node.Kind == NodeKind.Core && state != RuntimeState.Live)
                node.Caption = StateCaption();
        }

        /// <summary>Runs <paramref name="node"/>'s report, never letting a failing reporter break the window.</summary>
        private static bool TryReport(Node node, MonitorReport report, bool detailed, out string error)
        {
            error = null;
            report.Begin(detailed);

            try
            {
                if (node.Source != null)
                    node.Source.Report(report);
                else
                    node.StaticReport?.Invoke(report);

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                report.Begin(detailed);
                return false;
            }
        }

        private string StateCaption() => state switch
        {
            RuntimeState.Live => core != null ? core.WovenSubsystems.Count + " woven" : "live",
            RuntimeState.Deploying => "deploying",
            RuntimeState.Waiting => "waiting",
            _ => "offline",
        };
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
            RefreshOnLayout();

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
                lastDetailRefresh = -1d;
                GUIUtility.keyboardControl = 0;
                Repaint();
            }

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        /// <summary>
        /// Refreshes the graph at 10 Hz and the selected node's report at 4 Hz, only on Layout, so Layout and Repaint
        /// always draw the same content.
        /// </summary>
        private void RefreshOnLayout()
        {
            bool playing = EditorApplication.isPlaying;
            double now = EditorApplication.timeSinceStartup;

            if (coreNode == null || playing != wasPlaying)
            {
                wasPlaying = playing;
                lastModelRefresh = lastDetailRefresh = -1d;
            }

            if (Event.current.type != EventType.Layout && coreNode != null)
                return;

            if (lastModelRefresh < 0d || (playing && now - lastModelRefresh >= ModelInterval))
            {
                RefreshModel();
                lastModelRefresh = now;
            }

            var node = SelectedNode();

            if (lastDetailRefresh < 0d || detailKey != node.Key || (playing && now - lastDetailRefresh >= DetailInterval))
            {
                detailKey = node.Key;
                detailError = null;

                if (node.CanReport)
                    TryReport(node, detailReport, true, out detailError);
                else
                    detailReport.Begin(true);

                lastDetailRefresh = now;
            }
        }

        private Node SelectedNode()
        {
            if (selectedKey != CoreKey)
            {
                foreach (var node in nodes)
                {
                    if (node.Key == selectedKey)
                        return node;
                }

                selectedKey = CoreKey;
            }

            return coreNode;
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
            var hovered = rect.Contains(current.mousePosition) ? NodeAt(current.mousePosition) : null;

            hoveredKey = hovered?.Key;

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
                float textWidth = Mathf.Min(Theme.Measure(node.Caption, captionStyle).x, 150f);
                var anchor = node.Centre + outward * (node.Radius + 10f) + new Vector2(outward.x * textWidth * 0.5f, outward.y * 4f);

                Theme.Draw(new Rect(anchor.x - textWidth * 0.5f - 2f, anchor.y - 7f, textWidth + 4f, 14f), Theme.Fit(node.Caption, captionStyle, textWidth),
                    captionStyle, node.Alert ? Bad : InkDim);
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
            Theme.Draw(new Rect(x + 16f, y, 100f, 14f), "Service", legendStyle, InkFaint);

            x += 80f;
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

            var node = SelectedNode();

            GUILayout.BeginArea(rect);
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginHorizontal();
            GUILayout.Space(PanelPadding);
            GUILayout.BeginVertical(GUILayout.Width(PanelWidth - PanelPadding * 2f - 4f));
            GUILayout.Space(16f);

            var title = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            Theme.Draw(title, Theme.Fit(node.Label, titleStyle, title.width), titleStyle, Ink);

            var subtitle = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            Theme.Draw(subtitle, Theme.Fit(node.Subtitle, subtitleStyle, subtitle.width), subtitleStyle, InkDim);
            GUILayout.Space(4f);

            if (node.Kind == NodeKind.Subsystem)
                DrawSubsystemFacts(node);

            if (!node.Live && !string.IsNullOrEmpty(node.Description))
            {
                GUILayout.Space(10f);
                Note(node.Description, InkFaint);
            }

            if (node.CanReport)
                DrawReport(detailReport, detailError);

            if (node.Kind == NodeKind.Core)
            {
                DrawBudgetDetails();
                DrawMemoryDetails();
                DrawRenderingDetails();
            }

            GUILayout.Space(18f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>What the window can tell about any subsystem from its type alone.</summary>
        private void DrawSubsystemFacts(Node node)
        {
            var type = node.Type;

            Section("Subsystem");
            Row("Status", node.Live ? "Woven and linked" : EditorApplication.isPlaying ? "Not woven" : "Not running", node.Live ? Good : InkDim);

            if (type == null)
                return;

            Row("Type", type.FullName);
            Row("Assembly", type.Assembly.GetName().Name);
            Row("ID", "0x" + (node.Source is IThreadlinkSubsystem subsystem ? subsystem.ID : HashFunctions.ToXxHash32(type.FullName)).ToString("X8"));

            var lifecycle = new List<string>(4);
            if (typeof(IAddressablesPreloader).IsAssignableFrom(type)) lifecycle.Add("Preloads");
            lifecycle.Add("Boots");
            if (typeof(IInitializable).IsAssignableFrom(type)) lifecycle.Add("Initialises");
            if (typeof(IDisposable).IsAssignableFrom(type)) lifecycle.Add("Disposes");

            Row("Lifecycle", string.Join(" · ", lifecycle));
        }

        /// <summary>Renders a report's lines. Nothing here knows which subsystem or service wrote them.</summary>
        private void DrawReport(MonitorReport report, string error)
        {
            if (error != null)
            {
                Section("Report failed");
                Note(error, Bad);
                return;
            }

            var lines = report.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                switch (line.Kind)
                {
                    case MonitorLineKind.Section:
                        Section(line.Label);
                        break;

                    case MonitorLineKind.Field:
                        Row(line.Label ?? string.Empty, line.Value ?? string.Empty, ToneColor(line.Tone));
                        break;

                    case MonitorLineKind.Reference:
                        ObjectRow(line.Label ?? string.Empty, line.Target, line.Value);
                        break;

                    case MonitorLineKind.Meter:
                        Row(line.Label ?? string.Empty, line.Value ?? string.Empty, ToneColor(line.Tone));
                        Bar(line.Fraction, line.Tone == MonitorTone.Normal ? InkDim : ToneColor(line.Tone));
                        break;

                    case MonitorLineKind.Note:
                        Note(line.Label ?? string.Empty, line.Tone == MonitorTone.Normal ? InkDim : ToneColor(line.Tone));
                        GUILayout.Space(2f);
                        break;
                }
            }
        }

        private void DrawBudgetDetails()
        {
            Section("Frame budget");

            var popup = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            Theme.Draw(new Rect(popup.x, popup.y, popup.width * 0.5f, popup.height), "Target", keyStyle, Ink);

            var options = new string[TargetFrameRates.Length];
            options[0] = "Auto (" + ResolveTargetFrameRate(0) + " fps)";

            for (int i = 1; i < options.Length; i++)
                options[i] = TargetFrameRates[i] + " fps";

            targetFrameRateIndex = EditorGUI.Popup(new Rect(popup.xMax - 120f, popup.y + 1f, 120f, 18f), targetFrameRateIndex, options);

            float budget = BudgetMs();
            Row("Budget", budget.ToString("0.0") + " ms");

            if (!EditorApplication.isPlaying)
            {
                Note("Frame metrics are sampled in Play Mode.", InkFaint);
                return;
            }

            float usage = budget > 0f ? mainThreadMs / budget : 0f;
            Row("Main thread", mainThreadMs.ToString("0.0") + " ms  ·  " + Mathf.RoundToInt(usage * 100f) + "%", UsageColor(usage));
            Bar(usage, UsageColor(usage));
            Row("Headroom", (budget - mainThreadMs).ToString("0.0") + " ms", budget - mainThreadMs < 0f ? Bad : InkDim);
            Row("Frame rate", fps.ToString("0") + " fps");

            GUILayout.Space(6f);
            Sparkline(budget);
            Note("Main thread time over the last 12 seconds, against the budget line. In the Editor it includes Editor work.", InkFaint);
        }

        private void DrawMemoryDetails()
        {
            Section("Memory");

            if (!EditorApplication.isPlaying)
            {
                Note("Memory is sampled in Play Mode.", InkFaint);
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
            Note("Editor figures include the Editor itself. Profile a development build for numbers to budget against.", InkFaint);
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
            Theme.Draw(rect, Theme.Fit((title ?? string.Empty).ToUpperInvariant(), sectionStyle, rect.width), sectionStyle, InkFaint);
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
        private void ObjectRow(string key, UnityObject target, string value)
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
        private static Color ToneColor(MonitorTone tone) => tone switch
        {
            MonitorTone.Muted => InkFaint,
            MonitorTone.Good => Good,
            MonitorTone.Caution => Caution,
            MonitorTone.Bad => Bad,
            _ => InkDim,
        };

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

        /// <summary>Weaver, Linker, Register or Subsystem: the most specific Threadlink base <paramref name="type"/> derives from.</summary>
        private static string Kind(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;

                var definition = current.GetGenericTypeDefinition();

                if (definition == typeof(global::Threadlink.Core.Weaver<,>)) return "Weaver of " + current.GetGenericArguments()[1].Name;
                if (definition == typeof(global::Threadlink.Core.Linker<,>)) return "Linker of " + current.GetGenericArguments()[1].Name;
                if (definition == typeof(global::Threadlink.Core.Register<,>)) return "Register of " + current.GetGenericArguments()[1].Name;
            }

            return "Subsystem";
        }
        #endregion
    }
}
