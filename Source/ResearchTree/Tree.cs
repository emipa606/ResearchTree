// Tree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Tree
{
    public static bool Initialized;

    public static IntVec2 Size = IntVec2.Zero;

    private static List<Node> _nodes;

    private static List<Edge<Node, Node>> _edges;

    private static List<TechLevel> _relevantTechLevels;

    private static Dictionary<TechLevel, IntRange> _techLevelBounds;

    private static bool _initializing;

    public static bool OrderDirty;

    public static bool FirstLoadDone;

    public static Dictionary<TechLevel, IntRange> TechLevelBounds
    {
        get
        {
            if (_techLevelBounds == null)
            {
                throw new Exception("TechLevelBounds called before they are set.");
            }

            return _techLevelBounds;
        }
    }

    public static List<TechLevel> RelevantTechLevels
    {
        get
        {
            if (_relevantTechLevels == null)
            {
                _relevantTechLevels = (from TechLevel tl in Enum.GetValues(typeof(TechLevel))
                    where DefDatabase<ResearchProjectDef>.AllDefsListForReading.Any(rp => rp.techLevel == tl)
                    select tl).ToList();
            }

            return _relevantTechLevels;
        }
    }

    public static List<Node> Nodes
    {
        get
        {
            if (_nodes == null)
            {
                PopulateNodes();
            }

            return _nodes;
        }
    }

    public static List<Edge<Node, Node>> Edges
    {
        get
        {
            if (_edges == null)
            {
                throw new Exception("Trying to access edges before they are initialized.");
            }

            return _edges;
        }
    }

    public static void Reset(bool alsoZoom)
    {
        Messages.Message("Fluffy.ResearchTree.ResolutionChange".Translate(), MessageTypeDefOf.NeutralEvent);

        Size = IntVec2.Zero;
        _nodes = null;
        _edges = null;
        _relevantTechLevels = null;
        _techLevelBounds = null;
        _initializing = false;
        Initialized = false;
        OrderDirty = false;
        FirstLoadDone = false;
        if (MainTabWindow_ResearchTree.Instance != null)
        {
            if (alsoZoom)
            {
                MainTabWindow_ResearchTree.Instance.ResetZoomLevel();
            }

            MainTabWindow_ResearchTree.Instance._viewRect_InnerDirty = true;
            MainTabWindow_ResearchTree.Instance._viewRectDirty = true;
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == 1)
        {
            LongEventHandler.QueueLongEvent(Assets.StartLoadingWorker, "ResearchPal.BuildingResearchTreeAsync", true,
                null);
        }
    }

    public static void Initialize()
    {
        if (Initialized || _initializing)
        {
            return;
        }

        _initializing = true;

        if (FluffyResearchTreeMod.instance?.Settings?.LoadType == 1)
        {
            try
            {
                Log.Message("[ResearchTree]: CheckPrerequisites");
                CheckPrerequisites();
                Log.Message("[ResearchTree]: CreateEdges");
                CreateEdges();
                Log.Message("[ResearchTree]: HorizontalPositions");
                HorizontalPositions();
                Log.Message("[ResearchTree]: NormalizeEdges");
                NormalizeEdges();
                Log.Message("[ResearchTree]: Collapse");
                Collapse();
                Log.Message("[ResearchTree]: MinimizeCrossings");
                MinimizeCrossings();
                Log.Message("[ResearchTree]: MinimizeEdgeLength");
                MinimizeEdgeLength();
                Log.Message("[ResearchTree]: RemoveEmptyRows");
                RemoveEmptyRows();
                Initialized = true;
                Log.Message("[ResearchTree]: Done");
                return;
            }
            catch (Exception)
            {
                Log.Message("[ResearchTree]: Error initializing research tree, will retry.", true);
            }

            return;
        }

        LongEventHandler.QueueLongEvent(CheckPrerequisites, "Fluffy.ResearchTree.PreparingTree.Setup", false, null);
        LongEventHandler.QueueLongEvent(CreateEdges, "Fluffy.ResearchTree.PreparingTree.Setup", false, null);
        LongEventHandler.QueueLongEvent(HorizontalPositions, "Fluffy.ResearchTree.PreparingTree.Setup", false,
            null);
        LongEventHandler.QueueLongEvent(NormalizeEdges, "Fluffy.ResearchTree.PreparingTree.Setup", false, null);
        LongEventHandler.QueueLongEvent(Collapse, "Fluffy.ResearchTree.PreparingTree.CrossingReduction", false,
            null);
        LongEventHandler.QueueLongEvent(MinimizeCrossings, "Fluffy.ResearchTree.PreparingTree.CrossingReduction",
            false, null);
        LongEventHandler.QueueLongEvent(MinimizeEdgeLength, "Fluffy.ResearchTree.PreparingTree.LayoutNew", false,
            null);
        LongEventHandler.QueueLongEvent(RemoveEmptyRows, "Fluffy.ResearchTree.PreparingTree.LayoutNew", false, null);
        LongEventHandler.QueueLongEvent(delegate { Initialized = true; }, "Fluffy.ResearchTree.PreparingTree.LayoutNew",
            false, null);
        LongEventHandler.QueueLongEvent(MainTabWindow_ResearchTree.Instance.Notify_TreeInitialized,
            "Fluffy.ResearchTree.RestoreQueue", false, null);
    }

    private static void RemoveEmptyRows()
    {
        var y = 1;
        while (y <= Size.z)
        {
            if (Row(y).NullOrEmpty())
            {
                foreach (var item in Nodes.Where(n => n.Y > y))
                {
                    item.Y--;
                }
            }
            else
            {
                y++;
            }
        }
    }

    private static void MinimizeEdgeLength()
    {
        var edgeLengthSweepLocal = false;
        var num = 0;
        var num2 = 2;
        var num3 = 50;
        while ((!edgeLengthSweepLocal || num2 > 0) && num < num3)
        {
            edgeLengthSweepLocal = EdgeLengthSweep_Local(num++);
            if (!edgeLengthSweepLocal)
            {
                num2--;
            }
        }

        num = 0;
        num2 = 2;
        while (num2 > 0 && num < num3)
        {
            if (!EdgeLengthSweep_Global())
            {
                num2--;
            }
        }
    }

    private static bool EdgeLengthSweep_Global()
    {
        var num = EdgeLength();
        for (var i = 2; i <= Size.x; i++)
        {
            EdgeLengthSweep_Global_Layer(i, true);
        }

        return EdgeLength() < num;
    }

    private static bool EdgeLengthSweep_Local(int iteration)
    {
        var num = EdgeLength();
        if (iteration % 2 == 0)
        {
            for (var i = 2; i <= Size.x; i++)
            {
                EdgeLengthSweep_Local_Layer(i, true);
            }
        }
        else
        {
            for (var num2 = Size.x - 1; num2 >= 0; num2--)
            {
                EdgeLengthSweep_Local_Layer(num2, false);
            }
        }

        return EdgeLength() < num;
    }

    private static void EdgeLengthSweep_Global_Layer(int l, bool @in)
    {
        var num = EdgeLength(l, @in);
        var num2 = Crossings(l);
        if (Math.Abs(num) < Constants.Epsilon)
        {
            return;
        }

        foreach (var item in Layer(l, true))
        {
            var nodes = item.Nodes;
            if (!nodes.Any())
            {
                continue;
            }

            var num3 = Mathf.Min(item.Y, nodes.Min(n => n.Y));
            var num4 = Mathf.Max(item.Y, nodes.Max(n => n.Y));
            if (num3 == num4 && num3 == item.Y)
            {
                continue;
            }

            for (var i = num3; i <= num4; i++)
            {
                if (i == item.Y)
                {
                    continue;
                }

                var node = NodeAt(l, i);
                if (node != null)
                {
                    if (!TrySwap(item, node))
                    {
                        continue;
                    }

                    if (Crossings(l) > num2)
                    {
                        TrySwap(node, item);
                        continue;
                    }

                    var num5 = EdgeLength(l, @in);
                    if (num - num5 < Constants.Epsilon)
                    {
                        TrySwap(node, item);
                    }
                    else
                    {
                        num = num5;
                    }

                    continue;
                }

                var y = item.Y;
                item.Y = i;
                if (Crossings(l) > num2)
                {
                    item.Y = y;
                    continue;
                }

                var num6 = EdgeLength(l, @in);
                if (num - num6 < Constants.Epsilon)
                {
                    item.Y = y;
                }
                else
                {
                    num = num6;
                }
            }
        }
    }

    private static void EdgeLengthSweep_Local_Layer(int l, bool @in)
    {
        var num = @in ? l - 1 : l + 1;
        var num2 = Crossings(num);
        foreach (var item in Layer(l, true))
        {
            foreach (var item2 in @in ? item.InEdges : item.OutEdges)
            {
                var num3 = item2.Length;
                var node = @in ? item2.In : item2.Out;
                if (node.X != num)
                {
                    Log.Warning("{0} is not at layer {1}", node, num);
                }

                var num4 = Mathf.Min(item.Y, node.Y);
                var num5 = Mathf.Max(item.Y, node.Y);
                if (num4 == num5 && num4 == item.Y)
                {
                    continue;
                }

                for (var i = num4; i <= num5; i++)
                {
                    if (i == node.Y)
                    {
                        continue;
                    }

                    var node2 = NodeAt(num, i);
                    if (node2 != null)
                    {
                        if (!TrySwap(node, node2))
                        {
                            continue;
                        }

                        if (Crossings(num) > num2)
                        {
                            TrySwap(node2, node);
                            continue;
                        }

                        var length = item2.Length;
                        if (num3 - length < Constants.Epsilon)
                        {
                            TrySwap(node2, node);
                        }
                        else
                        {
                            num3 = length;
                        }

                        continue;
                    }

                    var y = node.Y;
                    node.Y = i;
                    if (Crossings(num) > num2)
                    {
                        node.Y = y;
                        continue;
                    }

                    var length2 = item2.Length;
                    if (num3 - length2 < Constants.Epsilon)
                    {
                        node.Y = y;
                    }
                    else
                    {
                        num3 = length2;
                    }
                }
            }
        }
    }

    public static void HorizontalPositions()
    {
        var relevantTechLevels = RelevantTechLevels;
        var num = 1;
        var num2 = 50;
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

    private static void NormalizeEdges()
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

    private static void CreateEdges()
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

    public static void CheckPrerequisites()
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

            Verse.Log.Message("Tried fixing research prerequisite issues for 10 iterations, aborting.");
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

            Log.Warning("\tredundant prerequisites for {0}: {1}. Removing.", researchNode.Research.LabelCap,
                string.Join(", ", enumerable.Select(r => r.LabelCap).ToArray()));
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

            Log.Warning("\t{0} has a lower techlevel than (one of) it's prerequisites, increasing.",
                node.Research.defName);
            node.Research.techLevel = node.Research.prerequisites.Max(r => r.techLevel);
            returnValue = false;
            foreach (var researchNode in node.Children)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Log.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its parents has changed tech-level.");
                queue.Enqueue(researchNode);
            }

            foreach (var researchNode in node.Parents)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Log.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its children has changed tech-level.");
                queue.Enqueue(researchNode);
            }
        }

        return returnValue;
    }

    private static void PopulateNodes()
    {
        // filter Anomaly DLC research
        var allDefsListForReading =
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => def.knowledgeCategory == null);
        // find hidden nodes (nodes that have themselves as a prerequisite)
        var hidden = allDefsListForReading.Where(p => p.prerequisites?.Contains(p) ?? false);
        // find locked nodes (nodes that have a hidden node as a prerequisite)
        var second = allDefsListForReading.Where(p => p.Ancestors().Intersect(hidden).Any());
        var researchList = allDefsListForReading.Except(hidden).Except(second).ToList();
        _nodes = [];
        Assets.TotalAmountOfResearch = researchList.Count;
        var iterator = 0;
        foreach (var def in researchList)
        {
            _nodes.Add(new ResearchNode(def, iterator));
            iterator++;
        }
    }

    private static void Collapse()
    {
        _ = Size;
        for (var i = 1; i <= Size.x; i++)
        {
            var list = Layer(i, true);
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

    [Conditional("DEBUG")]
    internal static void DebugDraw()
    {
        foreach (var node in Nodes)
        {
            foreach (var outNode in node.OutNodes)
            {
                Widgets.DrawLine(node.Right, outNode.Left, Color.white, 1f);
            }
        }
    }

    public static void Draw(Rect visibleRect)
    {
        foreach (var relevantTechLevel in RelevantTechLevels)
        {
            DrawTechLevel(relevantTechLevel, visibleRect);
        }

        foreach (var item in Edges.OrderBy(e => e.DrawOrder))
        {
            item.Draw(visibleRect);
        }

        foreach (var node in Nodes)
        {
            node.Draw(visibleRect);
        }
    }

    public static void DrawTechLevel(TechLevel techlevel, Rect visibleRect)
    {
        if (!TechLevelBounds.ContainsKey(techlevel))
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
            VerticalLabel(
                new Rect(num + (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                    visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                    Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
        }

        if (TechLevelBounds[techlevel].max < Size.x && num2 > visibleRect.xMin && num2 < visibleRect.xMax)
        {
            VerticalLabel(
                new Rect(num2 - (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                    visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                    Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
        }

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void VerticalLabel(Rect rect, string text)
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUIUtility.RotateAroundPivot(-90f, rect.center);
        GUI.matrix = matrix * GUI.matrix;
        Widgets.Label(rect, text);
        GUI.matrix = matrix;
    }

    private static Node NodeAt(int X, int Y)
    {
        return Enumerable.FirstOrDefault(Nodes, n => n.X == X && n.Y == Y);
    }

    public static void MinimizeCrossings()
    {
        for (var i = 1; i <= Size.x; i++)
        {
            var list = (from n in Layer(i)
                orderby n.Descendants.Count
                select n).ToList();
            for (var j = 0; j < list.Count; j++)
            {
                list[j].Y = j + 1;
            }
        }

        var barymetricSweep = false;
        var num = 0;
        var num2 = 2;
        var num3 = 50;
        while ((!barymetricSweep || num2 > 0) && num < num3)
        {
            barymetricSweep = BarymetricSweep(num++);
            if (!barymetricSweep)
            {
                num2--;
            }
        }

        num = 0;
        num2 = 2;
        while (num2 > 0 && num < num3)
        {
            if (!GreedySweep(num++))
            {
                num2--;
            }
        }
    }

    private static bool GreedySweep(int iteration)
    {
        var num = Crossings();
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

        return Crossings() < num;
    }

    private static void GreedySweep_Layer(int l)
    {
        var num = Crossings(l);
        if (num == 0)
        {
            return;
        }

        var list = Layer(l, true);
        for (var i = 0; i < list.Count - 1; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (!TrySwap(list[i], list[j]))
                {
                    continue;
                }

                var num2 = Crossings(l);
                if (num2 < num)
                {
                    num = num2;
                }
                else
                {
                    TrySwap(list[j], list[i]);
                }
            }
        }
    }

    private static bool TrySwap(Node A, Node B)
    {
        if (A.X != B.X)
        {
            Verse.Log.Warning($"Can't swap {A} and {B}, nodes on different layers");
            return false;
        }

        (A.Y, B.Y) = (B.Y, A.Y);
        return true;
    }

    private static bool BarymetricSweep(int iteration)
    {
        var num = Crossings();
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

        return Crossings() < num;
    }

    private static void BarymetricSweep_Layer(int layer, bool left)
    {
        var orderedEnumerable =
            from n in Layer(layer).ToDictionary(n => n, n => GetBarycentre(n, left ? n.InNodes : n.OutNodes))
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

    private static float GetBarycentre(Node node, List<Node> neighbours)
    {
        if (neighbours.NullOrEmpty())
        {
            return node.Yf;
        }

        return neighbours.Sum(n => n.Yf) / neighbours.Count;
    }

    private static int Crossings()
    {
        var num = 0;
        for (var i = 1; i < Size.x; i++)
        {
            num += Crossings(i, true);
        }

        return num;
    }

    private static float EdgeLength()
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

    private static float EdgeLength(int layer)
    {
        if (layer == 0)
        {
            return EdgeLength(layer, false);
        }

        if (layer == Size.x)
        {
            return EdgeLength(layer, true);
        }

        return EdgeLength(layer, true) * EdgeLength(layer, false);
    }

    private static int Crossings(int layer, bool @in)
    {
        var list = (from e in Layer(layer).SelectMany(n => !@in ? n.OutEdges : n.InEdges)
            orderby e.In.Y, e.Out.Y
            select e).ToList();
        if (list.Count < 2)
        {
            return 0;
        }

        var num = 0;
        for (var i = 0; i < list.Count - 1; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (list[j].Out.Y < list[i].Out.Y)
                {
                    num++;
                }
            }
        }

        return num;
    }

    private static float EdgeLength(int layer, bool @in)
    {
        var list = (from e in Layer(layer).SelectMany(n => !@in ? n.OutEdges : n.InEdges)
            orderby e.In.Y, e.Out.Y
            select e).ToList();
        if (list.NullOrEmpty())
        {
            return 0f;
        }

        return list.Sum(e => e.Length) * (!@in ? 1 : 2);
    }

    public static List<Node> Layer(int depth, bool ordered = false)
    {
        if (!ordered || !OrderDirty)
        {
            return Nodes.Where(n => n.X == depth).ToList();
        }

        _nodes = (from n in Nodes
            orderby n.X, n.Y
            select n).ToList();
        OrderDirty = false;

        return Nodes.Where(n => n.X == depth).ToList();
    }

    public static List<Node> Row(int Y)
    {
        return Nodes.Where(n => n.Y == Y).ToList();
    }

    public new static string ToString()
    {
        var stringBuilder = new StringBuilder();
        for (var i = 1; i <= Nodes.Max(n => n.X); i++)
        {
            stringBuilder.AppendLine($"Layer {i}:");
            foreach (var item in Layer(i, true))
            {
                stringBuilder.AppendLine($"\t{item}");
                stringBuilder.AppendLine(
                    $"\t\tAbove: {string.Join(", ", item.InNodes.Select(a => a.ToString()).ToArray())}");
                stringBuilder.AppendLine(
                    $"\t\tBelow: {string.Join(", ", item.OutNodes.Select(b => b.ToString()).ToArray())}");
            }
        }

        return stringBuilder.ToString();
    }

    public static void DebugStatus()
    {
        Log.Message($"duplicated positions:\n {string.Join("\n", (from n in Nodes
            where Nodes.Any(n2 => n != n2 && n.X == n2.X && n.Y == n2.Y)
            select $"{n.X}, {n.Y}: {n.Label}").ToArray())}");
        Log.Message($"out-of-bounds nodes:\n{string.Join("\n", (from n in Nodes
            where n.X < 1 || n.Y < 1
            select n.ToString()).ToArray())}");
    }

    public static void WaitForInitialization()
    {
        if (Initialized)
        {
            return;
        }

        if (_initializing)
        {
            while (_initializing)
            {
            }

            return;
        }

        Initialize();
    }
}