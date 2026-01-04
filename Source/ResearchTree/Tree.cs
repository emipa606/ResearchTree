// Tree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Tree
{
    public static bool Initialized;

    public static bool NoTabsSelected { get; private set; }

    private static bool _reopenResearchTabAfterInit = true;

    public static IntVec2 Size = IntVec2.Zero;

    private static List<Node> _nodes;

    private static List<Edge<Node, Node>> _edges;

    private static List<TechLevel> _relevantTechLevels;

    private static Dictionary<TechLevel, IntRange> _techLevelBounds;

    private static volatile bool _initializing;

    public static bool OrderDirty;

    public static bool FirstLoadDone;

    private static readonly Dictionary<ResearchProjectDef, Node> ResearchToNodesCache = [];

    private static bool _loggedInitialDraw;

    // --- caches for hot paths ---
    private static List<Node>[] _layerBuckets;                 // [0..L]
    private static List<Edge<Node, Node>>[] _inEdgesPerLayer;  // [0..L]
    private static List<Edge<Node, Node>>[] _outEdgesPerLayer; // [0..L]

    // Layer y-slot lookup: _layerSlots[x][y - 1] => Node
    private static Node[][] _layerSlots;

    private static readonly List<Node> VisibleNodesBuffer = new(256);

    private static readonly List<CollapsedEdge>[] _edgeDrawBuckets =
    {
        new List<CollapsedEdge>(64),
        new List<CollapsedEdge>(64),
        new List<CollapsedEdge>(64),
        new List<CollapsedEdge>(64)
    };

    private static List<CollapsedEdge> _collapsedEdges;

    private sealed class CollapsedEdge
    {
        private readonly List<DummyNode> _via;

        public CollapsedEdge(Node start, Node end, List<DummyNode> via)
        {
            Start = start;
            End = end;
            _via = via ?? [];

            MinLayer = Start?.X ?? 0;
            MaxLayer = Start?.X ?? 0;

            if (_via.Count > 0)
            {
                MinLayer = Math.Min(MinLayer, _via.Min(n => n.X));
                MaxLayer = Math.Max(MaxLayer, _via.Max(n => n.X));
            }

            if (End != null)
            {
                MinLayer = Math.Min(MinLayer, End.X);
                MaxLayer = Math.Max(MaxLayer, End.X);
            }
        }

        public Node Start { get; }

        public Node End { get; }

        public int MinLayer { get; }

        public int MaxLayer { get; }

        private Color EdgeColor => End?.EdgeColor ?? Color.white;

        private static Vector2 AnchorLeft(Node node, Node other)
        {
            if (node is DummyNode)
            {
                var center = node.Rect.center;
                return new Vector2(center.x, center.y);
            }

            if (other != null)
            {
                var rect = node.Rect;
                if (other.X < node.X - Constants.Epsilon)
                {
                    return new Vector2(rect.xMin, rect.center.y);
                }

                if (other.Yf > node.Yf + Constants.Epsilon)
                {
                    return new Vector2(rect.center.x, rect.yMax);
                }

                if (other.Yf < node.Yf - Constants.Epsilon)
                {
                    return new Vector2(rect.center.x, rect.yMin);
                }
            }

            return node.Left;
        }

        private static Vector2 AnchorRight(Node node, Node other)
        {
            if (node is DummyNode)
            {
                var center = node.Rect.center;
                return new Vector2(center.x, center.y);
            }

            if (other != null)
            {
                var rect = node.Rect;
                if (other.X > node.X + Constants.Epsilon)
                {
                    return new Vector2(rect.xMax, rect.center.y);
                }

                if (other.Yf > node.Yf + Constants.Epsilon)
                {
                    return new Vector2(rect.center.x, rect.yMax);
                }

                if (other.Yf < node.Yf - Constants.Epsilon)
                {
                    return new Vector2(rect.center.x, rect.yMin);
                }
            }

            return node.Right;
        }

        private static float ColumnLeft(int column)
        {
            return (column - 1) * (Constants.NodeSize.x + Constants.NodeMargins.x);
        }

        private static float ColumnRight(int column)
        {
            return ColumnLeft(column) + Constants.NodeSize.x;
        }

        private static void DrawHorizontal(float y, float x1, float x2, Color color)
        {
            var width = Math.Abs(x2 - x1);
            if (width <= Constants.Epsilon)
            {
                return;
            }

            FastGUI.DrawTextureFast(
                new Rect(Math.Min(x1, x2), y - 2f, width, 4f),
                Assets.Lines.EW,
                color);
        }

        private static void DrawVertical(float x, float y1, float y2, Color color)
        {
            var height = Math.Abs(y2 - y1);
            if (height <= Constants.Epsilon)
            {
                return;
            }

            var yMin = Math.Min(y1, y2);
            FastGUI.DrawTextureFast(new Rect(x - 2f, yMin, 4f, height), Assets.Lines.NS, color);
        }

        private static void DrawSegment(Node from, Node to, Color color, bool drawHub)
        {
            var start = AnchorRight(from, to);
            var end = AnchorLeft(to, from);

            var horizontalSpan = Math.Abs(end.x - start.x);
            var verticalSpan = Math.Abs(end.y - start.y);

            if (verticalSpan <= Constants.Epsilon)
            {
                DrawHorizontal(start.y, start.x, end.x, color);
            }
            else if (horizontalSpan <= Constants.Epsilon)
            {
                if (!TryDrawDetouredVertical(from, to, start, end, color))
                {
                    DrawVertical(start.x, start.y, end.y, color);
                }
            }
            else
            {
                float corridorX;
                if (to.X > from.X)
                {
                    corridorX = ColumnRight(from.X) + (Constants.NodeMargins.x / 2f);
                }
                else if (to.X < from.X)
                {
                    corridorX = ColumnLeft(from.X) - (Constants.NodeMargins.x / 2f);
                }
                else
                {
                    corridorX = (start.x + end.x) / 2f;
                }

                DrawHorizontal(start.y, start.x, corridorX, color);
                DrawVertical(corridorX, start.y, end.y, color);
                DrawHorizontal(end.y, corridorX, end.x, color);
            }

            if (drawHub)
            {
                FastGUI.DrawTextureFast(
                    new Rect(end.x - Constants.HubSize, end.y - 8f, Constants.HubSize, Constants.HubSize),
                    Assets.Lines.End,
                    color);
            }
        }

        internal bool HiddenBySkipCompleted()
        {
            if (!SkipCompletedSetting)
            {
                return false;
            }

            var left = FindRealResearchEndpoint(Start, backward: true);
            var right = FindRealResearchEndpoint(End, backward: false);

            var leftDone = left?.Research?.IsFinished ?? false;
            var rightDone = right?.Research?.IsFinished ?? false;

            return leftDone || rightDone;
        }

        public int DrawOrder
        {
            get
            {
                if (End?.Highlighted == true)
                {
                    return 3;
                }

                if (End?.Completed == true)
                {
                    return 2;
                }

                return End?.Available == true ? 1 : 0;
            }
        }

        public bool ShouldDraw(Rect visibleRect)
        {
            if (Start == null || End == null)
            {
                return false;
            }

            if (!Start.IsVisible || !End.IsVisible)
            {
                return false;
            }

            if (HiddenBySkipCompleted())
            {
                return false;
            }

            return IsEdgeVisible(visibleRect, Start.Rect, End.Rect);
        }

        public bool IntersectsLayers(int minLayer, int maxLayer)
        {
            return MaxLayer >= minLayer && MinLayer <= maxLayer;
        }

        public void Draw(Rect visibleRect)
        {
            if (!ShouldDraw(visibleRect) || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var nodes = new List<Node>(_via.Count + 2) { Start };
            nodes.AddRange(_via);
            nodes.Add(End);

            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var from = nodes[i];
                var to = nodes[i + 1];
                var drawHub = i == nodes.Count - 2;
                DrawSegment(from, to, EdgeColor, drawHub);
            }
        }

        public static CollapsedEdge FromTerminalEdge(Edge<Node, Node> edge)
        {
            var via = new List<DummyNode>();
            var current = edge.In;
            while (current is DummyNode dummy && dummy.InEdges.Count == 1)
            {
                via.Add(dummy);
                current = dummy.InEdges[0].In;
            }

            via.Reverse();
            var start = current ?? edge.In;
            return new CollapsedEdge(start, edge.Out, via);
        }

        private static bool TryDrawDetouredVertical(Node from, Node to, Vector2 start, Vector2 end, Color color)
        {
            if (from == null || to == null)
            {
                return false;
            }

            if (!SegmentBlocked(start.x, start.y, end.x, end.y, from, to))
            {
                return false;
            }

            var minY = Math.Min(start.y, end.y);
            var maxY = Math.Max(start.y, end.y);

            var leftCorridor = ColumnLeft(from.X) - (Constants.NodeMargins.x / 2f);
            var rightCorridor = ColumnRight(from.X) + (Constants.NodeMargins.x / 2f);

            var leftValid = CorridorIsClear(leftCorridor, start, end, from, to);
            var rightValid = CorridorIsClear(rightCorridor, start, end, from, to);

            if (!leftValid && !rightValid)
            {
                return false;
            }

            float corridorX;
            if (leftValid && rightValid)
            {
                var leftDensity = CountVisibleNodes(from.X - 1, minY, maxY, from, to);
                var rightDensity = CountVisibleNodes(from.X + 1, minY, maxY, from, to);
                corridorX = leftDensity <= rightDensity ? leftCorridor : rightCorridor;
            }
            else
            {
                corridorX = leftValid ? leftCorridor : rightCorridor;
            }

            DrawHorizontal(start.y, start.x, corridorX, color);
            DrawVertical(corridorX, start.y, end.y, color);
            DrawHorizontal(end.y, corridorX, end.x, color);

            return true;
        }

        private static bool CorridorIsClear(float corridorX, Vector2 start, Vector2 end, Node from, Node to)
        {
            if (float.IsNaN(corridorX))
            {
                return false;
            }

            if (SegmentBlocked(start.x, start.y, corridorX, start.y, from, to))
            {
                return false;
            }

            if (SegmentBlocked(end.x, end.y, corridorX, end.y, from, to))
            {
                return false;
            }

            if (SegmentBlocked(corridorX, start.y, corridorX, end.y, from, to))
            {
                return false;
            }

            return true;
        }

        private static bool SegmentBlocked(float x1, float y1, float x2, float y2, Node from, Node to)
        {
            var width = Math.Abs(x2 - x1);
            var height = Math.Abs(y2 - y1);

            if (width <= Constants.Epsilon && height <= Constants.Epsilon)
            {
                return false;
            }

            Rect segmentRect;
            if (width <= Constants.Epsilon)
            {
                segmentRect = new Rect(x1 - 2f, Math.Min(y1, y2), 4f, height);
            }
            else if (height <= Constants.Epsilon)
            {
                segmentRect = new Rect(Math.Min(x1, x2), y1 - 2f, width, 4f);
            }
            else
            {
                segmentRect = new Rect(Math.Min(x1, x2), Math.Min(y1, y2), width, height);
            }

            foreach (var node in Nodes)
            {
                if (node == null || node == from || node == to)
                {
                    continue;
                }

                if (!node.IsVisible)
                {
                    continue;
                }

                if (node.Rect.Overlaps(segmentRect))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountVisibleNodes(int column, float minY, float maxY, Node from, Node to)
        {
            if (column <= 0)
            {
                return 0;
            }

            var nodesInColumn = layer(column);
            if (nodesInColumn == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < nodesInColumn.Count; i++)
            {
                var node = nodesInColumn[i];
                if (node == null || node == from || node == to)
                {
                    continue;
                }

                if (!node.IsVisible)
                {
                    continue;
                }

                var rect = node.Rect;
                if (rect.yMax <= minY || rect.yMin >= maxY)
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }

    private const float CullPadding = 120f;
    private const string InitializePerformancePrefix = "Tree.Initialize::";
    private const int DummyTraversalGuard = 256;

    private static Dictionary<TechLevel, IntRange> TechLevelBounds
    {
        get
        {
            if (_techLevelBounds != null)
            {
                return _techLevelBounds;
            }

            Logging.Error("TechLevelBounds called before they are set.");
            return null;
        }
    }

    public static List<TechLevel> RelevantTechLevels
    {
        get
        {
            _relevantTechLevels ??= (from TechLevel tl in Enum.GetValues(typeof(TechLevel))
                where DefDatabase<ResearchProjectDef>.AllDefsListForReading.Any(rp => rp.techLevel == tl)
                select tl).OrderBy(tl => tl).ToList();

            return _relevantTechLevels;
        }
    }

    private static ResearchTabDef GetProjectTab(ResearchProjectDef def)
    {
        if (def == null)
        {
            return null;
        }

        var type = def.GetType();

        var property = type.GetProperty("tab")
                       ?? type.GetProperty("researchTab")
                       ?? type.GetProperty("researchTabDef");
        if (property != null && typeof(ResearchTabDef).IsAssignableFrom(property.PropertyType))
        {
            return property.GetValue(def) as ResearchTabDef;
        }

        var field = type.GetField("tab")
                    ?? type.GetField("researchTab")
                    ?? type.GetField("researchTabDef");
        if (field != null && typeof(ResearchTabDef).IsAssignableFrom(field.FieldType))
        {
            return field.GetValue(def) as ResearchTabDef;
        }

        try
        {
            return ResearchTabDefOf.Main;
        }
        catch
        {
            return null;
        }
    }

    // Whether completed projects should be hidden when rendering the tree.
    private static bool SkipCompletedSetting => FluffyResearchTreeMod.instance?.Settings?.SkipCompleted ?? false;

    // Check whether a node should be hidden while the "skip completed" option is active.
    private static bool NodeHiddenBySkipCompleted(Node node)
    {
        if (!SkipCompletedSetting)
        {
            return false;
        }

        return node is ResearchNode researchNode && researchNode.Research != null && researchNode.Research.IsFinished;
    }

    // Walk along dummy nodes until the next research node is encountered.
    private static ResearchNode FindRealResearchEndpoint(Node n, bool backward)
    {
        var current = n;
        var guard = DummyTraversalGuard;

        while (current is not ResearchNode && guard-- > 0)
        {
            var edges = backward ? current?.InEdges : current?.OutEdges;
            if (edges.NullOrEmpty())
            {
                return null;
            }

            current = backward ? edges[0].In : edges[0].Out;
        }

        return current as ResearchNode;
    }

    // Hide edge chains that connect two completed research nodes when the skip setting is active.
    private static bool EdgeHiddenBySkipCompleted<TIn, TOut>(Edge<TIn, TOut> e)
        where TIn : Node where TOut : Node
    {
        if (!SkipCompletedSetting)
        {
            return false;
        }

        var left = FindRealResearchEndpoint(e.In, backward: true);
        var right = FindRealResearchEndpoint(e.Out, backward: false);

        var leftDone = left?.Research?.IsFinished ?? false;
        var rightDone = right?.Research?.IsFinished ?? false;

        return leftDone || rightDone;
    }

    public static List<Node> Nodes
    {
        get
        {
            if (_nodes == null)
            {
                populateNodes();
            }

            return _nodes;
        }
    }

    private static List<Edge<Node, Node>> Edges
    {
        get
        {
            if (_edges != null)
            {
                return _edges;
            }

            Logging.Error("Trying to access edges before they are initialized.");
            return null;
        }
    }

    public static Node ResearchToNode(ResearchProjectDef research)
    {
        var node = ResearchToNodesCache.TryGetValue(research);
        if (node != null)
        {
            return node;
        }

        _ = Nodes.OfType<ResearchNode>();
        return ResearchToNodesCache.TryGetValue(research);
    }

    public static void Reset(bool alsoZoom)
    {
        Messages.Message("Fluffy.ResearchTree.ResolutionChange".Translate(), MessageTypeDefOf.NeutralEvent);

        Queue.Notify_TreeWillReset();

        NoTabsSelected = false;

        Size = IntVec2.Zero;
        _nodes = null;
        ResearchToNodesCache.Clear();
        _edges = null;
        _relevantTechLevels = null;
        _techLevelBounds = null;
        _initializing = false;
        Initialized = false;
        OrderDirty = false;
        FirstLoadDone = false;
        _collapsedEdges = null;
        MainTabWindow_ResearchTree.InvalidateTreeRectCache();
        if (MainTabWindow_ResearchTree.Instance != null)
        {
            if (alsoZoom)
            {
                MainTabWindow_ResearchTree.Instance.ResetZoomLevel();
            }

            MainTabWindow_ResearchTree.Instance.ViewRectInnerDirty = true;
            MainTabWindow_ResearchTree.Instance.ViewRectDirty = true;
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeLoadInBackground)
        {
            LongEventHandler.QueueLongEvent(Initialize, "ResearchPal.BuildingResearchTreeAsync", false,
                null);
        }
    }
    private static void ProfiledStep(string label, Action step)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            step();
        }
        finally
        {
            sw.Stop();
            Logging.Performance(label, sw.ElapsedMilliseconds, 250);
        }
    }

    private static void QueueProfiledLongEvent(Action step, string label, string textKey, bool doAsynchronously = false,
        Action<Exception> extraAction = null)
    {
        LongEventHandler.QueueLongEvent(() => ProfiledStep(label, step), textKey, doAsynchronously, extraAction);
    }

    public static void Initialize()
    {
        if (FluffyResearchTreeMod.instance?.Settings?.LoadType == Constants.LoadTypeDoNotGenerateResearchTree
            || Initialized || _initializing)
        {
            return;
        }

        _initializing = true;

        if (FluffyResearchTreeMod.instance?.Settings?.LoadType == Constants.LoadTypeLoadInBackground)
        {
            try
            {
                ProfiledStep($"{InitializePerformancePrefix}CheckPrerequisites", CheckPrerequisites);
                ProfiledStep($"{InitializePerformancePrefix}CreateEdges", createEdges);
                ProfiledStep($"{InitializePerformancePrefix}HorizontalPositions", horizontalPositions);
                ProfiledStep($"{InitializePerformancePrefix}NormalizeEdges", normalizeEdges);
                ProfiledStep($"{InitializePerformancePrefix}BuildCollapsedEdges", BuildCollapsedEdges);
                ProfiledStep($"{InitializePerformancePrefix}BuildBuckets", BuildBuckets);
                ProfiledStep($"{InitializePerformancePrefix}Collapse", collapse);
                ProfiledStep($"{InitializePerformancePrefix}MinimizeCrossings", minimizeCrossings);
                ProfiledStep($"{InitializePerformancePrefix}MinimizeEdgeLength", minimizeEdgeLength);
                ProfiledStep($"{InitializePerformancePrefix}RemoveEmptyRows", removeEmptyRows);

                Logging.Message("Done");
                Initialized = true;
            }
            catch (Exception ex)
            {
                Logging.Error("Error initializing research tree, will retry." + ex, true);
            }

            _initializing = false;
            _reopenResearchTabAfterInit = true;
            return;
        }

        QueueProfiledLongEvent(CheckPrerequisites, $"{InitializePerformancePrefix}CheckPrerequisites",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(createEdges, $"{InitializePerformancePrefix}CreateEdges",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(horizontalPositions, $"{InitializePerformancePrefix}HorizontalPositions",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(normalizeEdges, $"{InitializePerformancePrefix}NormalizeEdges",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(BuildCollapsedEdges, $"{InitializePerformancePrefix}BuildCollapsedEdges",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        // Build buckets and edge caches before running layout optimizations.
        QueueProfiledLongEvent(BuildBuckets, $"{InitializePerformancePrefix}BuildBuckets",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(collapse, $"{InitializePerformancePrefix}Collapse",
            "Fluffy.ResearchTree.PreparingTree.CrossingReduction");
        QueueProfiledLongEvent(minimizeCrossings, $"{InitializePerformancePrefix}MinimizeCrossings",
            "Fluffy.ResearchTree.PreparingTree.CrossingReduction");
        QueueProfiledLongEvent(minimizeEdgeLength, $"{InitializePerformancePrefix}MinimizeEdgeLength",
            "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(removeEmptyRows, $"{InitializePerformancePrefix}RemoveEmptyRows",
            "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(() =>
            {
                Initialized = true;
                _initializing = false;
                Logging.Message("Done");
            }, $"{InitializePerformancePrefix}Finalize", "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(Queue.Notify_TreeReinitialized,
            $"{InitializePerformancePrefix}NotifyQueueReinitialized", "Fluffy.ResearchTree.RestoreQueue");
        QueueProfiledLongEvent(MainTabWindow_ResearchTree.Instance.Notify_TreeInitialized,
            $"{InitializePerformancePrefix}NotifyTreeInitialized", "Fluffy.ResearchTree.RestoreQueue");
        if (_reopenResearchTabAfterInit)
        {
            // open tab
            QueueProfiledLongEvent(() => { Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research); },
                $"{InitializePerformancePrefix}ToggleResearchTab", "Fluffy.ResearchTree.RestoreQueue");
        }
        _reopenResearchTabAfterInit = true;
    }

    // Build per-layer caches for nodes and edges using the current horizontal layout.
    private static void BuildBuckets()
    {
        // Use the maximum current layer index (safer than relying on Size.x).
        int maxLayer = 0;
        if (!Nodes.NullOrEmpty())
        {
            maxLayer = Nodes.Max(n => n.X);
        }

        _layerBuckets = new List<Node>[maxLayer + 1];
        _inEdgesPerLayer = new List<Edge<Node, Node>>[maxLayer + 1];
        _outEdgesPerLayer = new List<Edge<Node, Node>>[maxLayer + 1];

        for (int x = 0; x <= maxLayer; x++)
        {
            _layerBuckets[x] = new List<Node>(64);
            _inEdgesPerLayer[x] = new List<Edge<Node, Node>>(64);
            _outEdgesPerLayer[x] = new List<Edge<Node, Node>>(64);
        }

        // Populate layer buckets.
        foreach (var n in Nodes)
        {
            if (n.X >= 0 && n.X <= maxLayer)
            {
                _layerBuckets[n.X].Add(n);
            }
        }

        // Cache inbound and outbound edges per layer (Y positions can change later).
        for (int x = 0; x <= maxLayer; x++)
        {
            var bucket = _layerBuckets[x];
            if (bucket.Count == 0)
            {
                continue;
            }

            foreach (var node in bucket)
            {
                if (!node.InEdges.NullOrEmpty())
                {
                    _inEdgesPerLayer[x].AddRange(node.InEdges);
                }

                if (!node.OutEdges.NullOrEmpty())
                {
                    _outEdgesPerLayer[x].AddRange(node.OutEdges);
                }
            }
        }

        // Normalize the Y ordering within each layer and build the slot lookup table.
        _layerSlots = new Node[_layerBuckets.Length][];
        for (int x = 0; x < _layerBuckets.Length; x++)
        {
            var bucket = _layerBuckets[x];
            if (bucket == null || bucket.Count == 0)
            {
                _layerSlots[x] = Array.Empty<Node>();
                continue;
            }

            bucket.Sort((a, b) => a.Y.CompareTo(b.Y));
            for (int i = 0; i < bucket.Count; i++)
            {
                bucket[i].Y = i + 1;
            }

            _layerSlots[x] = bucket.ToArray();
        }
    }

    private static void removeEmptyRows()
    {
        // Determine the highest populated row, falling back to the nodes if Size.z is stale.
        int maxY = Size.z > 0
            ? Size.z
            : (Nodes.Count == 0 ? 0 : Nodes.Max(n => n.Y));

        if (maxY <= 0 || Nodes.Count == 0)
        {
            Size = new IntVec2(Size.x, 0);
            return;
        }

        // Track which rows contain actual research nodes so that we can drop
        // dummy-only rows that bloat the layout.
        var used = new bool[maxY + 1];
        foreach (var researchNode in Nodes.OfType<ResearchNode>())
        {
            int y = researchNode.Y;
            if ((uint)y <= (uint)maxY) used[y] = true;
        }

        // Build an oldY -> newY compression map that removes rows without
        // research content while keeping relative order intact for all nodes.
        var map = new int[maxY + 1];
        int removed = 0;
        for (int y = 1; y <= maxY; y++)
        {
            if (!used[y])
            {
                removed++;
            }

            map[y] = Math.Max(1, y - removed);
        }

        // Re-assign node Y positions using the compression map.
        foreach (var n in Nodes)
        {
            int y = n.Y;
            if ((uint)y <= (uint)maxY) n.Y = map[y];
        }

        // Update Size.z to the new row count.
        Size = new IntVec2(Size.x, Math.Max(0, maxY - removed));

        // Synchronize caches with the new Y ordering.
        if (_layerBuckets != null)
        {
            for (int x = 0; x < _layerBuckets.Length; x++)
            {
                var bucket = _layerBuckets[x];
                if (bucket == null || bucket.Count == 0) continue;
                bucket.Sort((a, b) => a.Y.CompareTo(b.Y));
                for (int i = 0; i < bucket.Count; i++) bucket[i].Y = i + 1;
            }
        }
        if (_layerSlots != null && _layerBuckets != null)
        {
            _layerSlots = new Node[_layerBuckets.Length][];
            for (int x = 0; x < _layerBuckets.Length; x++)
                _layerSlots[x] = (_layerBuckets[x] == null) ? Array.Empty<Node>() : _layerBuckets[x].ToArray();
        }
    }

    // Replace the ordering of a layer with a new sequence and keep caches in sync.
    private static void ApplyLayerOrder(int l, Node[] newOrder)
    {
        // Update the slot cache first.
        if (_layerSlots == null || (uint)l >= (uint)_layerSlots.Length)
            return;

        _layerSlots[l] = newOrder;

        // Mirror the change to the bucket cache.
        if (_layerBuckets != null && (uint)l < (uint)_layerBuckets.Length)
        {
            var bucket = _layerBuckets[l];
            bucket.Clear();
            bucket.AddRange(newOrder);
        }

        // Normalize Y so nodes remain 1..Count within the layer.
        for (int i = 0; i < newOrder.Length; i++)
            newOrder[i].Y = i + 1;
    }

    // Compute barycenter keys for layer l using inbound or outbound edges.
    private static float[] ComputeBarycenterKeys(int l, bool @in)
    {
        var slots = _layerSlots;
        var arr = (slots != null && (uint)l < (uint)slots.Length) ? slots[l] : null;
        if (arr == null || arr.Length == 0) return Array.Empty<float>();

        var keys = new float[arr.Length];

        if (@in)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var n = arr[i];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0)
                {
                    keys[i] = n.Y; // No neighbours, keep the original position.
                    continue;
                }

                int sum = 0;
                for (int k = 0; k < eList.Count; k++) sum += eList[k].In.Y;
                keys[i] = (float)sum / eList.Count;
            }
        }
        else
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var n = arr[i];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0)
                {
                    keys[i] = n.Y;
                    continue;
                }

                int sum = 0;
                for (int k = 0; k < eList.Count; k++) sum += eList[k].Out.Y;
                keys[i] = (float)sum / eList.Count;
            }
        }

        return keys;
    }

    private static long TotalEdgeLength_Int(bool @in)
    {
        if (_layerSlots == null || _layerSlots.Length == 0) return 0;
        long sum = 0;
        for (int l = 0; l < _layerSlots.Length; l++)
        {
            // EdgeLength returns a float accumulated from integer differences; round to long for totals.
            sum += (long)Math.Round(EdgeLength(l, @in));
        }
        return sum;
    }


    private static void minimizeEdgeLength()
    {
        // Tunable parameters.
        const int MAX_PAIR_ITERS = 12;   // Maximum number of iteration pairs (two local sweeps each).
        const int MIN_PAIR_ITERS = 2;    // Minimum number of pairs before early stopping is considered.
        const double PAIR_REL_EPS = 0.04; // Threshold for per-pair relative improvement.
        const int PAIR_ABS_EPS = 600;  // Threshold for per-pair absolute improvement in total Y distance.
        const double TARGET_CUM_REL = 0.65; // Stop once cumulative relative gain reaches this value.

        // Plateau detection: stop when relative gain stabilizes across the last N pairs.
        const int PLATEAU_SPAN = 3;    // Number of recent pairs to sample.
        const double PLATEAU_DELTA = 0.05; // Maximum relative fluctuation allowed within the window.

        // Runtime metrics.
        var sw = new Stopwatch();
        long localMs = 0;
        double cumRel = 0.0;               // Cumulative relative improvement.
        var lastPairRels = new Queue<double>(PLATEAU_SPAN);

        for (int pair = 0; pair < MAX_PAIR_ITERS; pair++)
        {
            // Compute the denominator: the sum of inbound and outbound edge lengths.
            long denomIn = TotalEdgeLength_Int(@in: true);
            long denomOut = TotalEdgeLength_Int(@in: false);
            long denom = Math.Max(1, denomIn + denomOut); // Guard against division by zero.

            // Execute a pair of sweeps: even iterations use inbound edges, odd iterations outbound.
            int gainIn, gainOut;

            sw.Restart();
            bool impIn = EdgeLengthSweep_Local(2 * pair, out gainIn);   // in
            bool impOut = EdgeLengthSweep_Local(2 * pair + 1, out gainOut);  // out
            sw.Stop();

            localMs += sw.ElapsedMilliseconds;

            int pairGain = gainIn + gainOut;           // Absolute gain from this pair.
            double pairRel = (double)pairGain / denom;   // Relative gain from this pair.

            Logging.Message($"[Profile] EdgeLengthSweep_Local pair={pair} took {sw.ElapsedMilliseconds} ms, " +
                            $"gainIn={gainIn}, gainOut={gainOut}, pairGain={pairGain}, pairRel={pairRel:P2}");

            // Track cumulative improvement.
            cumRel += pairRel;

            // Evaluate early stopping conditions.
            bool stopByPairRel = pair >= MIN_PAIR_ITERS && pairRel < PAIR_REL_EPS;
            bool stopByPairAbs = pair >= MIN_PAIR_ITERS && pairGain < PAIR_ABS_EPS;
            bool stopByTarget = cumRel >= TARGET_CUM_REL;

            // Check whether the recent gains have plateaued.
            bool stopByPlateau = false;
            if (PLATEAU_SPAN > 1)
            {
                if (lastPairRels.Count == PLATEAU_SPAN) lastPairRels.Dequeue();
                lastPairRels.Enqueue(pairRel);

                if (lastPairRels.Count == PLATEAU_SPAN)
                {
                    double min = double.MaxValue, max = double.MinValue;
                    foreach (var r in lastPairRels) { if (r < min) min = r; if (r > max) max = r; }
                    // Relative fluctuation compared to the window mean.
                    double mean = 0.0; foreach (var r in lastPairRels) mean += r; mean /= PLATEAU_SPAN;
                    if (mean > 0 && (max - min) / mean < PLATEAU_DELTA && pair >= MIN_PAIR_ITERS)
                        stopByPlateau = true;
                }
            }

            if (stopByPairRel || stopByPairAbs || stopByTarget || stopByPlateau)
            {
                Logging.Message(
                    $"[Profile] Local early-stop at pair={pair} " +
                    $"(pairRel={pairRel:P2}, pairGain={pairGain}, cumRel={cumRel:P2}, " +
                    $"by={(stopByPairRel ? "PAIR_REL" : stopByPairAbs ? "PAIR_ABS" : stopByTarget ? "TARGET_CUM" : "PLATEAU")})");
                break;
            }
        }

        Logging.Message($"[Profile] EdgeLengthSweep_Local(pair-mode) total {localMs} ms");

        var swg = Stopwatch.StartNew();
        EdgeLengthSweep_Global();
        swg.Stop();
        Logging.Message($"[Profile] EdgeLengthSweep_Global total {swg.ElapsedMilliseconds} ms");
    }


    private static void EdgeLengthSweep_Global()
    {
        if (_layerSlots == null || _layerSlots.Length == 0) return;

        // Run a few left-to-right and right-to-left passes.
        const int ROUNDS = 2;

        for (int round = 0; round < ROUNDS; round++)
        {
            // Left to right: use inbound edges from the previous layer.
            for (int l = 0; l < _layerSlots.Length; l++)
                EdgeLengthSweep_Global_Layer(l, @in: true);

            // Right to left: use outbound edges toward the next layer.
            for (int l = _layerSlots.Length - 1; l >= 0; l--)
                EdgeLengthSweep_Global_Layer(l, @in: false);
        }
    }


    private static bool EdgeLengthSweep_Local(int iter, out int totalGain)
    {
        bool useIn = (iter & 1) == 0;

        totalGain = 0;
        if (_layerSlots == null || _layerSlots.Length == 0)
            return false;

        bool improvedAny = false;
        for (int l = 0; l < _layerSlots.Length; l++)
        {
            var arr = _layerSlots[l];
            if (arr == null || arr.Length < 2) continue;

            int layerGain;
            if (EdgeLengthSweep_Local_Layer(l, useIn, out layerGain))
            {
                improvedAny = true;
                totalGain += layerGain;   // Accumulate actual gain for this layer.
            }
        }
        return improvedAny;
    }


    // Reorder layer l using barycentric sorting. Inbound edges use the previous layer; outbound use the next.
    private static void EdgeLengthSweep_Global_Layer(int l, bool @in)
    {
        var slots = _layerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return;

        // 1) Calculate barycentric keys.
        var keys = ComputeBarycenterKeys(l, @in);

        // 2) Clone the current ordering as baseline and candidate arrays.
        var baseOrder = (Node[])arr.Clone();
        var candOrder = (Node[])arr.Clone();

        // 3) Stable sort by key, falling back to the original index for ties.
        Array.Sort(candOrder, (a, b) =>
        {
            // Determine original indices within baseOrder.
            int ia = Array.IndexOf(baseOrder, a);
            int ib = Array.IndexOf(baseOrder, b);

            float ka = keys[ia];
            float kb = keys[ib];

            int c = ka.CompareTo(kb);
            if (c != 0) return c;
            return ia.CompareTo(ib);
        });

        // 4) Only keep the new order if it does not increase crossings.
        int beforeCross = Crossings(l);
        ApplyLayerOrder(l, candOrder);
        int afterCross = Crossings(l);
        if (afterCross > beforeCross)
        {
            // Roll back to the baseline order.
            ApplyLayerOrder(l, baseOrder);
        }
    }

    // Calculate edge-length delta for swapping adjacent nodes A (Y=a) and B (Y=b).
    // @in=true uses inbound edges, otherwise outbound edges. Returns after - before (negative is better).
    private static int DeltaEdgeLengthForAdjacentSwap(Node A, Node B, bool @in)
    {
        int aY = A.Y;
        int bY = B.Y;

        int before = 0, after = 0;

        if (@in)
        {
            var eA = A.InEdges;
            if (eA != null)
            {
                for (int k = 0; k < eA.Count; k++)
                {
                    int y = eA[k].In.Y;
                    before += Math.Abs(y - aY);
                    after += Math.Abs(y - bY);   // A moves to B's Y.
                }
            }

            var eB = B.InEdges;
            if (eB != null)
            {
                for (int k = 0; k < eB.Count; k++)
                {
                    int y = eB[k].In.Y;
                    before += Math.Abs(y - bY);
                    after += Math.Abs(y - aY);   // B moves to A's Y.
                }
            }
        }
        else
        {
            var eA = A.OutEdges;
            if (eA != null)
            {
                for (int k = 0; k < eA.Count; k++)
                {
                    int y = eA[k].Out.Y;
                    before += Math.Abs(y - aY);
                    after += Math.Abs(y - bY);
                }
            }

            var eB = B.OutEdges;
            if (eB != null)
            {
                for (int k = 0; k < eB.Count; k++)
                {
                    int y = eB[k].Out.Y;
                    before += Math.Abs(y - bY);
                    after += Math.Abs(y - aY);
                }
            }
        }

        return after - before;
    }

    private static bool EdgeLengthSweep_Local_Layer(int l, bool @in, out int layerGain)
    {
        layerGain = 0;

        var slots = _layerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return false;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return false;

        bool improvedLayer = false;
        const int PASSES = 1; // Increase for more aggressive swapping.

        for (int pass = 0; pass < PASSES; pass++)
        {
            for (int i = 0; i < arr.Length - 1; i++)
            {
                var A = arr[i];
                var B = arr[i + 1];

                int delta = DeltaEdgeLengthForAdjacentSwap(A, B, @in); // after - before
                if (delta < 0) // Negative delta means the swap shortens edges.
                {
                    trySwap(A, B);              // Keep caches and node positions synchronized.
                    improvedLayer = true;
                    layerGain += -delta;     // Accumulate the actual improvement (before - after).

                    if (i > 0) i--;             // Step back so the new pair is reconsidered.
                }
            }
        }
        return improvedLayer;
    }


    private static void horizontalPositions()
    {
        var relevantTechLevels = RelevantTechLevels;
        var num = 1;
        const int num2 = 50;
        bool setDepth;
        do
        {
            var depth = 1;
            setDepth = false;
            foreach (var techlevel2 in relevantTechLevels)
            {
                var enumerable = from n in Nodes.OfType<ResearchNode>()
                    where n.Research.techLevel == techlevel2
                    select n;
                if (!enumerable.Any())
                {
                    continue;
                }

                foreach (var item in enumerable)
                {
                    setDepth = item.SetDepth(depth) || setDepth;
                }

                depth = enumerable.Max(n => n.X) + 1;
            }
        } while (setDepth && num++ < num2);

        _techLevelBounds = new Dictionary<TechLevel, IntRange>();
        foreach (var techlevel in relevantTechLevels)
        {
            var source = from n in Nodes.OfType<ResearchNode>()
                where n.Research.techLevel == techlevel
                select n;
            if (!source.Any())
            {
                continue;
            }

            _techLevelBounds[techlevel] = new IntRange(source.Min(n => n.X) - 1, source.Max(n => n.X));
        }
    }

    private static void normalizeEdges()
    {
        foreach (var item3 in new List<Edge<Node, Node>>(Edges.Where(e => e.Span > 1)))
        {
            Edges.Remove(item3);
            item3.In.OutEdges.Remove(item3);
            item3.Out.InEdges.Remove(item3);
            var node = item3.In;
            var num = (item3.Out.Yf - item3.In.Yf) / item3.Span;
            for (var i = item3.In.X + 1; i < item3.Out.X; i++)
            {
                var dummyNode = new DummyNode
                {
                    X = i,
                    Yf = item3.In.Yf + (num * (i - item3.In.X))
                };
                var item = new Edge<Node, Node>(node, dummyNode);
                node.OutEdges.Add(item);
                dummyNode.InEdges.Add(item);
                _nodes.Add(dummyNode);
                Edges.Add(item);
                node = dummyNode;
            }

            var item2 = new Edge<Node, Node>(node, item3.Out);
            node.OutEdges.Add(item2);
            item3.Out.InEdges.Add(item2);
            Edges.Add(item2);
        }
    }

    private static void BuildCollapsedEdges()
    {
        _collapsedEdges ??= new List<CollapsedEdge>(Edges?.Count ?? 0);
        _collapsedEdges.Clear();

        if (Edges.NullOrEmpty())
        {
            return;
        }

        if (_collapsedEdges.Capacity < Edges.Count)
        {
            _collapsedEdges.Capacity = Edges.Count;
        }

        foreach (var node in Nodes)
        {
            if (node is not ResearchNode researchNode)
            {
                continue;
            }

            if (researchNode.InEdges.NullOrEmpty())
            {
                continue;
            }

            foreach (var edge in researchNode.InEdges)
            {
                if (edge == null || edge.Out != researchNode)
                {
                    continue;
                }

                _collapsedEdges.Add(CollapsedEdge.FromTerminalEdge(edge));
            }
        }
    }

    private static void createEdges()
    {
        if (_edges.NullOrEmpty())
        {
            _edges = [];
        }

        foreach (var item2 in Nodes.OfType<ResearchNode>())
        {
            if (item2.Research.prerequisites.NullOrEmpty())
            {
                continue;
            }

            foreach (var prerequisite in item2.Research.prerequisites)
            {
                ResearchNode researchNode = prerequisite;
                if (researchNode == null)
                {
                    continue;
                }

                var item = new Edge<Node, Node>(researchNode, item2);
                Edges.Add(item);
                item2.InEdges.Add(item);
                researchNode.OutEdges.Add(item);
            }
        }
    }

    private static void CheckPrerequisites()
    {
        var keepIterating = true;
        var iterator = 10;
        while (keepIterating)
        {
            if (checkPrerequisites())
            {
                keepIterating = false;
            }

            iterator--;
            if (iterator > 0)
            {
                continue;
            }

            Logging.Warning("Tried fixing research prerequisite issues for 10 iterations, aborting.");
            keepIterating = false;
        }
    }

    private static bool checkPrerequisites()
    {
        var queue = new Queue<ResearchNode>(Nodes.OfType<ResearchNode>());
        var returnValue = true;
        while (queue.Count > 0)
        {
            var researchNode = queue.Dequeue();
            if (researchNode.Research.prerequisites.NullOrEmpty())
            {
                continue;
            }

            var enumerable =
                researchNode.Research.prerequisites.SelectMany(r => r.Ancestors()).ToList().Intersect(researchNode
                    .Research.prerequisites);
            if (!enumerable.Any())
            {
                continue;
            }

            Logging.Warning(
                $"\tredundant prerequisites for {researchNode.Research.LabelCap}: {string.Join(", \n", enumerable.Select(r => r.LabelCap).ToArray())}. Removing.");
            foreach (var item in enumerable)
            {
                researchNode.Research.prerequisites.Remove(item);
            }

            returnValue = false;
        }

        queue = new Queue<ResearchNode>(Nodes.OfType<ResearchNode>());
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Research.prerequisites.NullOrEmpty() ||
                !node.Research.prerequisites.Any(r => (int)r.techLevel > (int)node.Research.techLevel))
            {
                continue;
            }

            Logging.Warning(
                $"\t{node.Research.defName} has a lower techlevel than (one of) it's prerequisites, increasing.");
            node.Research.techLevel = node.Research.prerequisites.Max(r => r.techLevel);
            returnValue = false;
            foreach (var researchNode in node.Children)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Logging.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its parents has changed tech-level.");
                queue.Enqueue(researchNode);
            }

            foreach (var researchNode in node.Parents)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Logging.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its children has changed tech-level.");
                queue.Enqueue(researchNode);
            }
        }

        return returnValue;
    }


    private static ResearchTabDef TryGetProjectTab(ResearchProjectDef def)
    {
        // Support mods that expose either "tab" or "researchTab" fields.
        var f = AccessTools.Field(typeof(ResearchProjectDef), "tab")
                ?? AccessTools.Field(typeof(ResearchProjectDef), "researchTab");
        return f?.GetValue(def) as ResearchTabDef;
    }

    private static void populateNodes()
    {
        NoTabsSelected = false;

        // Filter out Anomaly DLC research projects.
        var allDefsListForReading =
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => def.knowledgeCategory == null).ToArray();
        var hidden = allDefsListForReading.Where(p => p.prerequisites?.Contains(p) ?? false);
        var second = allDefsListForReading.Where(p => p.Ancestors().Intersect(hidden).Any());
        var baseResearchList = allDefsListForReading.Except(hidden).Except(second).ToList();
        var researchList = baseResearchList;

        // Filter by ResearchTabDef origin.
        var st = FluffyResearchTreeMod.instance?.Settings;
        if (st != null)
        {
            st.EnsureTabCache(); // Keep the tab cache up-to-date when mods change mid-run.

            var hasSelection = st.IncludedTabs != null && st.IncludedTabs.Count > 0;
            var hasKnownTabs = st.AllTabsCache != null && st.AllTabsCache.Count > 0;

            if (hasKnownTabs && !hasSelection)
            {
                NoTabsSelected = true;
                researchList = [];
            }
            else if (hasSelection)
            {
                NoTabsSelected = false;

                var validDefs = new HashSet<ResearchProjectDef>(baseResearchList);
                var includedDefs = new HashSet<ResearchProjectDef>();

                foreach (var def in baseResearchList)
                {
                    var tab = TryGetProjectTab(def);
                    // If no tab can be resolved, skip filtering to avoid removing the project.
                    if (tab == null || st.TabIncluded(tab))
                    {
                        includedDefs.Add(def);
                    }
                }

                foreach (var queuedProject in Queue.GetQueuedProjects())
                {
                    if (!validDefs.Contains(queuedProject))
                    {
                        continue;
                    }

                    includedDefs.Add(queuedProject);
                }

                if (includedDefs.Count > 0)
                {
                    var queue = new Queue<ResearchProjectDef>(includedDefs);

                    void enqueuePrerequisites(IEnumerable<ResearchProjectDef> prereqs)
                    {
                        if (prereqs == null)
                        {
                            return;
                        }

                        foreach (var pre in prereqs)
                        {
                            if (!validDefs.Contains(pre))
                            {
                                continue;
                            }

                            if (includedDefs.Add(pre))
                            {
                                queue.Enqueue(pre);
                            }
                        }
                    }

                    while (queue.Count > 0)
                    {
                        var def = queue.Dequeue();
                        enqueuePrerequisites(def.prerequisites);
                        enqueuePrerequisites(def.hiddenPrerequisites);
                    }
                }

                researchList = baseResearchList.Where(includedDefs.Contains).ToList();
            }
            else
            {
                NoTabsSelected = false;
            }
        }

        // Create nodes in parallel.
        _nodes = [];
        Assets.TotalAmountOfResearch = researchList.Count;

        Parallel.ForEach(researchList, (def, _, index) =>
        {
            var researchNode = new ResearchNode(def, (int)index);
            lock (_nodes)
            {
                _nodes.Add(researchNode);
                ResearchToNodesCache[def] = researchNode;
            }
        });
    }

    // Request a tree rebuild from inside Tree.cs.
    public static void RequestRebuild(bool resetZoom = true, bool reopenResearchTab = false)
    {
        // Reset refresh flags and cached state.
        Assets.RefreshResearch = false;

        _reopenResearchTabAfterInit = reopenResearchTab;

        // Reset clears caches, sizes, and marks the windows dirty. In background mode it also queues Initialize.
        Reset(alsoZoom: resetZoom);

        // Foreground mode rebuilds immediately; background mode already queued Initialize via Reset.
        var st = FluffyResearchTreeMod.instance?.Settings;
        if (st == null || st.LoadType != Constants.LoadTypeLoadInBackground)
        {
            Initialize();
        }
        else
        {
            LongEventHandler.QueueLongEvent(
                Queue.Notify_TreeReinitialized,
                "Fluffy.ResearchTree.RestoreQueue",
                false, null
            );
            // In background mode we need to enqueue the notification separately.
            LongEventHandler.QueueLongEvent(
                MainTabWindow_ResearchTree.Instance.Notify_TreeInitialized,
                "Fluffy.ResearchTree.RestoreQueue",
                false, null
            );
        }

        // Optionally reopen the research tab after rebuilding.
        if (reopenResearchTab)
        {
            // Toggle the tab if it is not currently visible.
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
        }
    }

    private static void collapse()
    {
        _ = Size;
        for (var i = 1; i <= Size.x; i++)
        {
            var list = layer(i, true);
            var num = 1;
            foreach (var item in list)
            {
                item.Y = num++;
            }
        }
    }

    public static void ResetNodeAvailabilityCache()
    {
        foreach (var node in Nodes)
        {
            if (node is ResearchNode researchNode)
            {
                researchNode.ClearInstanceCaches();
            }
        }
    }

    public static void Draw(Rect visibleRect)
    {
        Stopwatch drawTimer = null;
        if (!_loggedInitialDraw)
        {
            drawTimer = Stopwatch.StartNew();
        }

        // Draw tech levels (this is unaffected by the skip-completed filter).
        foreach (var relevantTechLevel in RelevantTechLevels)
        {
            drawTechLevel(relevantTechLevel, visibleRect);
        }

        if (Edges == null || Nodes == null || Nodes.Count == 0)
        {
            return;
        }

        getVisibleLayerRange(visibleRect, out var minLayer, out var maxLayer);
        var maxRow = getMaxRow();
        getVisibleRowRange(visibleRect, maxRow, out var minRow, out var maxRowVisible);

        var canUseCulling = maxLayer >= minLayer && maxRow > 0 && maxRowVisible >= minRow;

        if (canUseCulling)
        {
            collectVisibleEdges(minLayer, maxLayer);
            foreach (var bucket in _edgeDrawBuckets)
            {
                for (var i = 0; i < bucket.Count; i++)
                {
                    bucket[i].Draw(visibleRect);
                }
            }

            collectVisibleNodes(visibleRect, minLayer, maxLayer, minRow, maxRowVisible);
            for (var i = 0; i < VisibleNodesBuffer.Count; i++)
            {
                VisibleNodesBuffer[i].Draw(visibleRect);
            }

            if (drawTimer != null)
            {
                drawTimer.Stop();
                _loggedInitialDraw = true;
                Logging.Performance(
                    $"Tree.Draw initial cull path (nodes={Nodes.Count}, edges={Edges.Count})",
                    drawTimer.ElapsedMilliseconds, 250);
            }

            return;
        }

        if (_collapsedEdges != null)
        {
            foreach (var edge in _collapsedEdges)
            {
                edge?.Draw(visibleRect);
            }
        }

        foreach (var node in Nodes)
        {
            if (NodeHiddenBySkipCompleted(node))
            {
                continue;
            }

            if (!node.IsWithinViewport(visibleRect))
            {
                continue;
            }

            node.Draw(visibleRect);
        }

        if (drawTimer != null)
        {
            drawTimer.Stop();
            _loggedInitialDraw = true;
            Logging.Performance(
                $"Tree.Draw initial full path (nodes={Nodes.Count}, edges={Edges.Count})",
                drawTimer.ElapsedMilliseconds, 250);
        }
    }

    private static void getVisibleLayerRange(Rect visibleRect, out int minLayer, out int maxLayer)
    {
        var maxLayerCount = Size.x;
        if (maxLayerCount <= 0)
        {
            if (_layerSlots != null && _layerSlots.Length > 0)
            {
                maxLayerCount = _layerSlots.Length - 1;
            }
            else if (!Nodes.NullOrEmpty())
            {
                maxLayerCount = Nodes.Max(n => n.X);
            }
        }

        if (maxLayerCount <= 0)
        {
            minLayer = 1;
            maxLayer = 0;
            return;
        }

        var span = Constants.NodeSize.x + Constants.NodeMargins.x;
        var padding = Mathf.Max(span, CullPadding);
        var minX = Mathf.Max(0f, visibleRect.xMin - padding);
        var maxX = visibleRect.xMax + padding;

        minLayer = Mathf.Clamp(Mathf.FloorToInt(minX / span) + 1, 1, maxLayerCount);
        maxLayer = Mathf.Clamp(Mathf.FloorToInt(maxX / span) + 1, minLayer, maxLayerCount);
    }

    private static int getMaxRow()
    {
        var maxRow = Size.z;
        if (maxRow > 0)
        {
            return maxRow;
        }

        if (_layerSlots != null && _layerSlots.Length > 0)
        {
            for (var i = _layerSlots.Length - 1; i >= 0; i--)
            {
                var column = _layerSlots[i];
                if (column == null || column.Length == 0)
                {
                    continue;
                }

                var candidate = column[column.Length - 1].Y;
                if (candidate > 0)
                {
                    return candidate;
                }
            }
        }

        if (Nodes.NullOrEmpty())
        {
            return 0;
        }

        return Nodes.Max(n => n.Y);
    }

    private static void getVisibleRowRange(Rect visibleRect, int maxRow, out int minRow, out int maxRowVisible)
    {
        if (maxRow <= 0)
        {
            minRow = 1;
            maxRowVisible = 0;
            return;
        }

        var span = Constants.NodeSize.y + Constants.NodeMargins.y;
        var padding = Mathf.Max(span, CullPadding);
        var minY = Mathf.Max(0f, visibleRect.yMin - padding);
        var maxY = visibleRect.yMax + padding;

        minRow = Mathf.Clamp(Mathf.FloorToInt(minY / span) + 1, 1, maxRow);
        maxRowVisible = Mathf.Clamp(Mathf.FloorToInt(maxY / span) + 1, minRow, maxRow);
    }

    private static void collectVisibleEdges(int minLayer, int maxLayer)
    {
        for (var i = 0; i < _edgeDrawBuckets.Length; i++)
        {
            _edgeDrawBuckets[i].Clear();
        }

        var collapsed = _collapsedEdges;
        if (collapsed == null)
        {
            if (!Edges.NullOrEmpty())
            {
                BuildCollapsedEdges();
                collapsed = _collapsedEdges;
            }
            else
            {
                return;
            }
        }

        foreach (var edge in collapsed)
        {
            if (edge == null)
            {
                continue;
            }

            if (!edge.IntersectsLayers(minLayer, maxLayer))
            {
                continue;
            }

            if (edge.HiddenBySkipCompleted())
            {
                continue;
            }

            _edgeDrawBuckets[edge.DrawOrder].Add(edge);
        }
    }

    private static void collectVisibleNodes(Rect visibleRect, int minLayer, int maxLayer, int minRow, int maxRow)
    {
        VisibleNodesBuffer.Clear();

        if (_layerSlots == null || _layerSlots.Length == 0)
        {
            var nodes = Nodes;
            foreach (var node in nodes)
            {
                if (!node.IsVisible || NodeHiddenBySkipCompleted(node))
                {
                    continue;
                }

                if (!node.IsWithinViewport(visibleRect))
                {
                    continue;
                }

                VisibleNodesBuffer.Add(node);
            }

            return;
        }

        var minIndex = Mathf.Clamp(minLayer, 0, _layerSlots.Length - 1);
        var maxIndex = Mathf.Clamp(maxLayer, minIndex, _layerSlots.Length - 1);

        for (var layer = minIndex; layer <= maxIndex; layer++)
        {
            var column = _layerSlots[layer];
            if (column == null || column.Length == 0)
            {
                continue;
            }

            var start = findFirstNodeIndex(column, minRow);
            if (start < 0)
            {
                continue;
            }

            for (var i = start; i < column.Length; i++)
            {
                var node = column[i];
                if (node.Y > maxRow)
                {
                    break;
                }

                if (!node.IsVisible || NodeHiddenBySkipCompleted(node))
                {
                    continue;
                }

                if (!node.IsWithinViewport(visibleRect))
                {
                    continue;
                }

                VisibleNodesBuffer.Add(node);
            }
        }
    }

    private static int findFirstNodeIndex(Node[] nodes, int minRow)
    {
        var low = 0;
        var high = nodes.Length - 1;
        var result = -1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (nodes[mid].Y >= minRow)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    public static bool IsEdgeVisible<T1, T2>(Edge<T1, T2> edge, Rect visibleRect)
        where T1 : Node where T2 : Node
    {
        return IsEdgeVisible(visibleRect, edge.In.Rect, edge.Out.Rect);
    }

    public static bool IsEdgeVisible(Rect visibleRect, Rect inRect, Rect outRect)
    {
        const float MARGIN = 100f;
        var rect = new Rect(
            visibleRect.xMin - MARGIN, visibleRect.yMin - MARGIN,
            visibleRect.width + 2 * MARGIN, visibleRect.height + 2 * MARGIN);

        if (inRect.Overlaps(rect) || outRect.Overlaps(rect))
        {
            return true;
        }

        var a = inRect.center;
        var b = outRect.center;
        return LineIntersectsRect(a, b, rect);

        static bool LineIntersectsRect(Vector2 p1, Vector2 p2, Rect r)
        {
            if (p1.x < r.xMin && p2.x < r.xMin) return false;
            if (p1.x > r.xMax && p2.x > r.xMax) return false;
            if (p1.y < r.yMin && p2.y < r.yMin) return false;
            if (p1.y > r.yMax && p2.y > r.yMax) return false;

            if (r.Contains(p1) || r.Contains(p2)) return true;

            return SegmentsIntersect(p1, p2, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
        }

        static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

            var ab = b - a;
            var ac = c - a;
            var ad = d - a;
            var cd = d - c;
            var ca = a - c;
            var cb = b - c;

            var d1 = Cross(ab, ac);
            var d2 = Cross(ab, ad);
            var d3 = Cross(cd, ca);
            var d4 = Cross(cd, cb);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            bool OnColinear(Vector2 p, Vector2 q, Vector2 r) =>
                Mathf.Min(p.x, q.x) <= r.x && r.x <= Mathf.Max(p.x, q.x) &&
                Mathf.Min(p.y, q.y) <= r.y && r.y <= Mathf.Max(p.y, q.y);

            if (Mathf.Approximately(d1, 0) && OnColinear(a, b, c)) return true;
            if (Mathf.Approximately(d2, 0) && OnColinear(a, b, d)) return true;
            if (Mathf.Approximately(d3, 0) && OnColinear(c, d, a)) return true;
            if (Mathf.Approximately(d4, 0) && OnColinear(c, d, b)) return true;

            return false;
        }
    }


    private static void drawTechLevel(TechLevel techlevel, Rect visibleRect)
    {
        if (!TechLevelBounds.ContainsKey(techlevel))
        {
            return;
        }

        if (Assets.IsHiddenByTechLevelRestrictions(techlevel))
        {
            return;
        }

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var num = ((Constants.NodeSize.x + Constants.NodeMargins.x) * TechLevelBounds[techlevel].min) -
                  (Constants.NodeMargins.x / 2f);
        var num2 = ((Constants.NodeSize.x + Constants.NodeMargins.x) * TechLevelBounds[techlevel].max) -
                   (Constants.NodeMargins.x / 2f);
        GUI.color = Assets.TechLevelColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        if (TechLevelBounds[techlevel].min > 0 && num > visibleRect.xMin && num < visibleRect.xMax)
        {
            Widgets.DrawLine(new Vector2(num, visibleRect.yMin), new Vector2(num, visibleRect.yMax),
                Assets.TechLevelColor, 1f);
            verticalLabel(
                new Rect(num + (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                    visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                    Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
        }

        if (TechLevelBounds[techlevel].max < Size.x && num2 > visibleRect.xMin && num2 < visibleRect.xMax)
        {
            if (!Assets.IsHiddenByTechLevelRestrictions(techlevel + 1))
            {
                verticalLabel(
                    new Rect(num2 - (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                        visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                        Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
            }
        }

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void verticalLabel(Rect rect, string text)
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUIUtility.RotateAroundPivot(-90f, rect.center);
        GUI.matrix = matrix * GUI.matrix;
        Widgets.Label(rect, text);
        GUI.matrix = matrix;
    }

    private static Node nodeAt(int X, int Y)
    {
        var slots = _layerSlots;
        if (slots != null && (uint)X < (uint)slots.Length)
        {
            var arr = slots[X];
            if (arr != null && (uint)Y - 1 < (uint)arr.Length)
                return arr[Y - 1];
        }

        return Enumerable.FirstOrDefault(Nodes, n => n.X == X && n.Y == Y);
    }

    private static void minimizeCrossings()
    {
        // Tunable parameters.
        const int MAX_PASSES_BARY = 50;  // Matches the original upper limit.
        const int MAX_PASSES_GREEDY = 50;
        const int MIN_PASSES_BARY = 0;   // Raise for a more conservative barycentric phase.
        const int MIN_PASSES_GREEDY = 0;
        const int FAILS_QUOTA_BARY = 2;   // Allowed failures after the first barycentric success.
        const int FAILS_QUOTA_GREEDY = 2;   // Allowed failures for the greedy phase.

        // Pre-layout: preserve the original ordering logic.
        Parallel.For(1, Size.x + 1, i =>
        {
            var list = (from n in layer(i)
                        orderby n.Descendants.Count
                        select n).ToList();
            for (var j = 0; j < list.Count; j++) list[j].Y = j + 1;
        });

        var totalSw = new System.Diagnostics.Stopwatch();
        totalSw.Start();

        // Barycentric phase: require a success before counting failures toward the quota.
        var barySw = new System.Diagnostics.Stopwatch();
        int pass = 0;
        int baryFailsLeft = FAILS_QUOTA_BARY;
        bool seenBarySuccess = false;
        long baryMs = 0;

        while (pass < MAX_PASSES_BARY)
        {
            barySw.Restart();
            bool improved = barymetricSweep(pass++);
            barySw.Stop();
            baryMs += barySw.ElapsedMilliseconds;

            if (improved)
            {
                if (!seenBarySuccess) seenBarySuccess = true; // Start counting failures after the first success.
            }
            else
            {
                if (seenBarySuccess && pass > MIN_PASSES_BARY) baryFailsLeft--;
            }

            Logging.Message($"[Profile] CrossingSweep bary pass={pass - 1} took {barySw.ElapsedMilliseconds} ms, improved={improved}, failsLeft={(seenBarySuccess ? baryFailsLeft : FAILS_QUOTA_BARY)}");

            if (seenBarySuccess && pass >= MIN_PASSES_BARY && baryFailsLeft <= 0)
            {
                Logging.Message($"[Profile] CrossingSweep early-stop bary at pass={pass - 1} (after first success, FAILS_QUOTA reached)");
                break;
            }
        }

        // Greedy phase: stop once the allowed number of failed passes is reached.
        var greedySw = new System.Diagnostics.Stopwatch();
        pass = 0;
        int greedyFailsLeft = FAILS_QUOTA_GREEDY;
        long greedyMs = 0;

        while (pass < MAX_PASSES_GREEDY && greedyFailsLeft > 0)
        {
            greedySw.Restart();
            bool improved = greedySweep(pass++);
            greedySw.Stop();
            greedyMs += greedySw.ElapsedMilliseconds;

            if (!improved && pass > MIN_PASSES_GREEDY) greedyFailsLeft--;

            Logging.Message($"[Profile] CrossingSweep greedy pass={pass - 1} took {greedySw.ElapsedMilliseconds} ms, improved={improved}, failsLeft={greedyFailsLeft}");
        }

        totalSw.Stop();
        Logging.Message($"[Profile] CrossingSweep took {totalSw.ElapsedMilliseconds} ms (bary={baryMs} ms, greedy={greedyMs} ms)");
    }



    private static bool greedySweep(int iteration)
    {
        var num = crossings();
        if (iteration % 2 == 0)
        {
            for (var i = 1; i <= Size.x; i++)
            {
                GreedySweep_Layer(i);
            }
        }
        else
        {
            for (var num2 = Size.x; num2 >= 1; num2--)
            {
                GreedySweep_Layer(num2);
            }
        }

        return crossings() < num;
    }

    private static void GreedySweep_Layer(int l)
    {
        // Quick exit if no crossings exist.
        int best = Crossings(l);
        if (best == 0) return;

        var slots = _layerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return;

        bool improved = true;
        while (improved)
        {
            improved = false;
            // Only test adjacent pairs (i, i + 1).
            for (int i = 0; i < arr.Length - 1; i++)
            {
                var A = arr[i];
                var B = arr[i + 1];
                if (!trySwap(A, B)) continue;

                int cur = Crossings(l);
                if (cur < best)
                {
                    best = cur;
                    improved = true;
                    // arr references _layerSlots[l], so swapping updates the shared data.
                }
                else
                {
                    // Revert the swap when it doesn't help.
                    trySwap(B, A);
                }
            }
        }
    }


    private static bool trySwap(Node A, Node B)
    {
        if (A.X != B.X)
        {
            Logging.Warning($"Can't swap {A} and {B}, nodes on different layers");
            return false;
        }

        int l = A.X;
        int ia = A.Y - 1;
        int ib = B.Y - 1;

        // Swap entries in the slot cache.
        if (_layerSlots != null && (uint)l < (uint)_layerSlots.Length)
        {
            var arr = _layerSlots[l];
            if (arr != null && (uint)ia < (uint)arr.Length && (uint)ib < (uint)arr.Length)
                (arr[ia], arr[ib]) = (arr[ib], arr[ia]);
        }

        // Mirror the swap in the bucket cache to keep ordering consistent.
        if (_layerBuckets != null && (uint)l < (uint)_layerBuckets.Length)
        {
            var bucket = _layerBuckets[l];
            if (bucket != null && (uint)ia < (uint)bucket.Count && (uint)ib < (uint)bucket.Count)
                (bucket[ia], bucket[ib]) = (bucket[ib], bucket[ia]);
        }

        // Swap the Y coordinates on the nodes themselves.
        (A.Y, B.Y) = (B.Y, A.Y);
        return true;
    }


    private static bool barymetricSweep(int iteration)
    {
        var num = crossings();
        if (iteration % 2 == 0)
        {
            for (var i = 2; i <= Size.x; i++)
            {
                BarymetricSweep_Layer(i, true);
            }
        }
        else
        {
            for (var num2 = Size.x - 1; num2 > 0; num2--)
            {
                BarymetricSweep_Layer(num2, false);
            }
        }

        return crossings() < num;
    }

    private static void BarymetricSweep_Layer(int layer, bool left)
    {
        var orderedEnumerable =
            from n in Tree.layer(layer).ToDictionary(n => n, n => getBarycentre(n, left ? n.InNodes : n.OutNodes))
            orderby n.Value
            select n;
        var num = float.MinValue;
        var dictionary = new Dictionary<float, List<Node>>();
        foreach (var item in orderedEnumerable)
        {
            if (Math.Abs(item.Value - num) > Constants.Epsilon)
            {
                num = item.Value;
                dictionary[num] = [];
            }

            dictionary[num].Add(item.Key);
        }

        var num2 = 1;
        foreach (var item2 in dictionary)
        {
            var key = item2.Key;
            var count = item2.Value.Count;
            num2 = (int)Mathf.Max(num2, key - ((count - 1) / (float)2));
            foreach (var item3 in item2.Value)
            {
                item3.Y = num2++;
            }
        }
    }

    private static float getBarycentre(Node node, List<Node> neighbours)
    {
        if (neighbours.NullOrEmpty())
        {
            return node.Yf;
        }

        return neighbours.Sum(n => n.Yf) / neighbours.Count;
    }

    private static int crossings()
    {
        var num = 0;
        for (var i = 1; i < Size.x; i++)
        {
            num += Crossings(i, true);
        }

        return num;
    }

    private static float edgeLength()
    {
        var num = 0f;
        for (var i = 1; i < Size.x; i++)
        {
            num += EdgeLength(i, true);
        }

        return num;
    }

    private static int Crossings(int layer)
    {
        if (layer == 0)
        {
            return Crossings(layer, false);
        }

        if (layer == Size.x)
        {
            return Crossings(layer, true);
        }

        return Crossings(layer, true) + Crossings(layer, false);
    }

    // Fenwick tree and inversion-counting helpers for crossing detection.
    private sealed class Fenwick
    {
        private readonly int[] bit;
        public Fenwick(int n) { bit = new int[n + 2]; } // 1-based
        public void Add(int idx, int delta = 1)
        {
            for (int i = idx + 1; i < bit.Length; i += i & -i) bit[i] += delta;
        }
        public int SumPrefix(int idx)
        {
            int s = 0;
            for (int i = idx + 1; i > 0; i -= i & -i) s += bit[i];
            return s;
        }
    }

    private static int CountInversions(int[] arr)
    {
        if (arr.Length < 2) return 0;
        // Coordinate compression.
        var uniq = arr.Distinct().ToList();
        uniq.Sort();
        var index = new Dictionary<int, int>(uniq.Count);
        for (int i = 0; i < uniq.Count; i++) index[uniq[i]] = i;

        var ft = new Fenwick(uniq.Count + 2);
        int inv = 0, seen = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            int r = index[arr[i]];
            inv += seen - ft.SumPrefix(r);
            ft.Add(r, 1);
            seen++;
        }
        return inv;
    }

    private static int Crossings(int layer, bool @in)
    {
        var slots = _layerSlots;
        if (slots == null || (uint)layer >= (uint)slots.Length) return 0;
        var arr = slots[layer];
        if (arr == null || arr.Length < 2) return 0;

        // Collect Y positions of neighbouring nodes in the target layer.
        // Start with a generous capacity to avoid repeated growth.
        var targetY = new List<int>(arr.Length * 2);
        if (@in)
        {
            // Gather edges from the previous layer.
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++) targetY.Add(eList[k].In.Y);
            }
        }
        else
        {
            // Gather edges toward the next layer.
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++) targetY.Add(eList[k].Out.Y);
            }
        }

        if (targetY.Count < 2) return 0;
        return CountInversions(targetY.ToArray()); // Reuse the Fenwick-based inversion counter.
    }

    private static float EdgeLength(int layer, bool @in)
    {
        var slots = _layerSlots;
        if (slots == null || (uint)layer >= (uint)slots.Length) return 0f;
        var arr = slots[layer];
        if (arr == null || arr.Length == 0) return 0f;

        float sum = 0f;
        if (@in)
        {
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++)
                    sum += Math.Abs(eList[k].In.Y - n.Y);
            }
        }
        else
        {
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++)
                    sum += Math.Abs(eList[k].Out.Y - n.Y);
            }
        }
        return sum;
    }

    private static List<Node> layer(int depth, bool ordered = false)
    {
        if (_layerBuckets != null && depth >= 0 && depth < _layerBuckets.Length)
            return _layerBuckets[depth];

        // Fallback to the original behaviour when caches are unavailable.
        if (!ordered || !OrderDirty)
            return Nodes.Where(n => n.X == depth).ToList();

        _nodes = (from n in Nodes orderby n.X, n.Y select n).ToList();
        OrderDirty = false;
        return Nodes.Where(n => n.X == depth).ToList();
    }

    private static List<Node> row(int Y)
    {
        return Nodes.Where(n => n.Y == Y).ToList();
    }

    public static void WaitForInitialization()
    {
        if (Initialized)
        {
            return;
        }

        if (_initializing)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        Initialize();
        sw.Stop();
        Logging.Performance("Tree.WaitForInitialization", sw.ElapsedMilliseconds, 250);
    }
}