// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class Queue : GameComponent
{
    private static Queue _instance;

    private static Vector2 _sideScrollPosition = Vector2.zero;

    private static readonly MethodInfo AttemptBeginResearchMethodInfo =
        AccessTools.Method(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.AttemptBeginResearch),
            [typeof(ResearchProjectDef)]);

    private static readonly MainTabWindow_Research MainTabWindowResearchInstance =
        (MainTabWindow_Research)MainButtonDefOf.Research.TabWindow;

    public static ResearchNode _draggedNode;

    private readonly List<ResearchNode> _queue = [];
    
    private Dictionary<KnowledgeCategoryDef, List<ResearchNode>> _anomalyQueue;

    private List<ResearchProjectDef> _saveableQueue;

    public Queue(Game game)
    {
        _instance = this;
        EnsureAnomalyQueueInitialized();
    }
    
    // TODO: Determine the timing of this method invocation
    private void EnsureAnomalyQueueInitialized()
    {
        if (!ModsConfig.AnomalyActive)
        {
            return;
        }
        if (_anomalyQueue == null)
        {
            _anomalyQueue = new Dictionary<KnowledgeCategoryDef, List<ResearchNode>>();
            foreach (var def in DefDatabase<KnowledgeCategoryDef>.AllDefs)
            {
                _anomalyQueue[def] = new List<ResearchNode>();
            }
        }
        else
        {
            foreach (var knowledgeCategoryDef in _anomalyQueue.Keys)
            {
                _anomalyQueue[knowledgeCategoryDef].RemoveAll(x => !x.Research.IsAnomalyResearch());
            }
        }
    }

    private static int NumQueued => _instance._queue.Count + _instance._anomalyQueue.Values.Sum(anomalyQueue => anomalyQueue.Count);

    public static void TryDequeue(ResearchNode node)
    {
        if (_instance._queue.Contains(node) || 
            _instance._anomalyQueue.Values.Any(anomalyQueue => anomalyQueue.Contains(node)))
        {
            Dequeue(node);
        }
    }

    private static void Dequeue(ResearchNode node)
    {
        if (node.Research.IsAnomalyResearch())
        {
            Dequeue(node, _instance._anomalyQueue.Values.SelectMany(anomalyQueue => anomalyQueue).ToList());
            return;
        }
        Dequeue(node, _instance._queue);
    }

    private static void Dequeue(ResearchNode node, List<ResearchNode> researchNodeQueue)
    {
        var removeFirst = false;
        var indexOf = researchNodeQueue.IndexOf(node);
        if (indexOf >= 0)
        {
            researchNodeQueue.RemoveAt(indexOf);
            removeFirst = indexOf == 0;
        }

        node.QueueRect = Rect.zero;
        foreach (var item in researchNodeQueue.Where(n => n.GetMissingRequiredRecursive().Contains(node)).ToList())
        {
            indexOf = researchNodeQueue.IndexOf(item);
            if (indexOf < 0)
            {
                continue;
            }

            item.QueueRect = Rect.zero;
            researchNodeQueue.RemoveAt(indexOf);
            if (!removeFirst && indexOf == 0)
            {
                removeFirst = true;
            }
        }

        if (Find.ResearchManager.IsCurrentProject(node.Research))
        {
            Find.ResearchManager.StopProject(node.Research);
        }

        if (node.Research.IsAnomalyResearch())
        {
            foreach (var knowledgeCategoryDef in _instance._anomalyQueue.Keys)
            {
                _instance._anomalyQueue[knowledgeCategoryDef].Clear();
            }
            researchNodeQueue.Do(x => Enqueue(x, false));
        }

        // try to remove duplicate confirmation window
        Find.WindowStack.TryRemoveAssignableFromType(typeof(Dialog_MessageBox), false);

        if (removeFirst)
        {
            AttemptBeginResearch();
        }
    }

    private static void Enqueue(ResearchNode node, bool add = true)
    {
        if (node.Research.IsAnomalyResearch())
        {
            Enqueue(node, _instance._anomalyQueue[node.Research.knowledgeCategory]);
            return;
        }
        Enqueue(node, _instance._queue, add);
    }
    private static void Enqueue(ResearchNode node, List<ResearchNode> researchNodeQueue, bool add = true)
    {
        if (!add)
        {
            researchNodeQueue.Clear();
        }

        if (!researchNodeQueue.Contains(node))
        {
            researchNodeQueue.Add(node);
        }
    }

    private static void ReEnqueue(ResearchNode node)
    {
        if (node.Research.IsAnomalyResearch())
        {
            ReEnqueue(node, _instance._anomalyQueue[node.Research.knowledgeCategory]);
            return;
        }
        ReEnqueue(node, _instance._queue);
    }
    private static void ReEnqueue(ResearchNode node, List<ResearchNode> researchNodeQueue)
    {
        if (!researchNodeQueue.Contains(node))
        {
            researchNodeQueue.Insert(0, node);
            return;
        }

        var index = researchNodeQueue.IndexOf(node);
        for (var i = index; i > 0; i--)
        {
            researchNodeQueue[i] = researchNodeQueue[i - 1];
        }

        researchNodeQueue[0] = node;
    }

    public static void EnqueueRangeFirst(IEnumerable<ResearchNode> nodes)
    {
        var researchNodes = nodes.ToList();
        var researchOrderSorted = SortResearchNodes(researchNodes).ToList();

        if (IsEnqueueRangeFirstSameOrder(researchOrderSorted))
        {
            return;
        }

        var first = researchNodes.First();
        
        researchOrderSorted.Reverse();
        
        foreach (var item in researchOrderSorted)
        {
            ReEnqueue(item);
        }

        if (Find.ResearchManager.GetProject(first.Research.knowledgeCategory) == null ||
            !Find.ResearchManager.IsCurrentProject(first.Research))
        {
            AttemptBeginResearch();
        }

        UpdateNodeQueueRect();
    }

    public static void DoBeginResearch(ResearchNode node)
    {
        ReEnqueue(node);
        FocusStartedProject(node.Research);
        UpdateNodeQueueRect();
    }

    private static bool IsEnqueueRangeFirstSameOrder(IEnumerable<ResearchNode> nodes,
        bool nodesOrdered = true, bool warning = true)
    {
        if (nodes == null)
        {
            return false;
        }

        var researchNodes = nodes.ToList();
        var researchOrderSorted = nodesOrdered ? researchNodes.ToList() : SortResearchNodes(researchNodes).ToList();

        var first = researchNodes.First();
        var queue = first.Research.IsAnomalyResearch() ? _instance._anomalyQueue[first.Research.knowledgeCategory] : 
            _instance._queue;

        if (researchOrderSorted.Count > queue.Count)
        {
            return false;
        }

        var sameOrder = !researchOrderSorted.Where((t, i) => t != queue[i]).Any();

        if (!sameOrder)
        {
            return false;
        }

        if (warning)
        {
            Messages.Message("Fluffy.ResearchTree.CannotMoveMore".Translate(researchOrderSorted.Last().Label), null,
                MessageTypeDefOf.RejectInput);
        }

        return true;
    }

    private static IEnumerable<ResearchNode> SortResearchNodes(IEnumerable<ResearchNode> nodes)
    {
        return Tree.Instance.Initialized ? nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent) :
            nodes.OrderBy(node => node.Research.ResearchViewX).ThenBy(node => node.Research.ResearchViewY);
    }

    public static void EnqueueRange(IEnumerable<ResearchNode> nodes, bool add)
    {
        var researchNodes = SortResearchNodes(nodes).ToList();
        var first = researchNodes.First();
        var queue = first.Research.IsAnomalyResearch() ? _instance._anomalyQueue[first.Research.knowledgeCategory] : 
            _instance._queue;
        
        if (!add)
        {
            foreach (var t in queue.ToList())
            {
                Find.ResearchManager.StopProject(t.Research);
            }

            queue.Clear();
        }

        var firstEnqueue = queue.Empty();
        foreach (var item in researchNodes)
        {
            Enqueue(item);
        }

        if (firstEnqueue)
        {
            AttemptBeginResearch();
        }

        UpdateNodeQueueRect();
    }

    public static bool IsQueued(ResearchNode node)
    {
        return _instance._queue.Contains(node) || _instance._anomalyQueue.Values.Any(anomalyQueue => anomalyQueue.Contains(node));
    }

    public static void TryStartNext(ResearchProjectDef finished)
    {
        if (!IsQueued(finished.ResearchNode()))
        {
            // Filtered unlocked research that comes with the start
            return;
        }

        TryDequeue(finished.ResearchNode());
        var current = finished.IsAnomalyResearch() ? 
            _instance._anomalyQueue[finished.knowledgeCategory].FirstOrDefault() : _instance._queue.FirstOrDefault();
        AttemptBeginResearch();
        AttemptDoCompletionLetter(finished, current?.Research);
    }

    private static void AttemptDoCompletionLetter(ResearchProjectDef current, ResearchProjectDef next)
    {
        if (current is not { IsFinished: true })
        {
            return;
        }

        string text = "ResearchFinished".Translate(current.LabelCap);
        string text2 = current.LabelCap + "\n\n" + current.description;
        LetterDef letter;
        if (next != null)
        {
            text2 += "\n\n" + "Fluffy.ResearchTree.NextInQueue".Translate(next.LabelCap);
            letter = LetterDefOf.PositiveEvent;
        }
        else
        {
            text2 += "\n\n" + "Fluffy.ResearchTree.NextInQueue".Translate("Fluffy.ResearchTree.None".Translate());
            letter = LetterDefOf.NeutralEvent;
        }
        Find.LetterStack.ReceiveLetter(text, text2, letter);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _saveableQueue = _queue.Concat(_anomalyQueue.Values.SelectMany(q => q)).Select(node => node.Research).ToList();
        }

        // Used only for assignment, initialization is moved to LoadedGame().
        // Because when initializing here, the initialization of the discovered anomaly research has not been completed (IsHidden = false).
        Scribe_Collections.Look(ref _saveableQueue, "Queue", LookMode.Def);
    }

    public static void DrawOrderLabel(Rect visibleRect, ResearchNode node)
    {
        DrawLabels(visibleRect, node,
            node.Research.IsAnomalyResearch() ? _instance._anomalyQueue[node.Research.knowledgeCategory] : _instance._queue);
    }

    private static void DrawLabels(Rect visibleRect, ResearchNode node, List<ResearchNode> queue)
    {
        var index = queue.IndexOf(node);
        if (!node.IsVisible(visibleRect) || index < 0)
        {
            return;
        }
        var rect = new Rect(node.Rect.xMax - (Constants.QueueLabelSize / 2f),
            node.Rect.yMin + ((node.Rect.height - Constants.QueueLabelSize) / 2f),
            Constants.QueueLabelSize,
            Constants.QueueLabelSize);
        var color = Assets.ColorCompleted[node.Research.techLevel];
        var num = index + 1;
        var background = num > 1 ? Assets.ColorUnavailable[node.Research.techLevel] : color;
        DrawLabel(rect, color, background, num.ToString());
    }

    public static void DrawLabelForMainButton(Rect rect)
    {
        var currentStart = rect.xMax - Constants.SmallQueueLabelSize - Constants.Margin;
        if (!Tree.Instance.Initialized && !FluffyResearchTreeMod.instance.Settings.DoNotGenerateResearchTree())
        {
            DrawLabel(
                new Rect(currentStart, 0f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize)
                    .CenteredOnYIn(rect), Color.yellow,
                Color.grey, "..", "Fluffy.ResearchTree.StillLoading".Translate());
            TooltipHandler.TipRegion(rect, "Fluffy.ResearchTree.LoadingWait".Translate());
            return;
        }

        if (NumQueued <= 0)
        {
            return;
        }

        DrawLabel(
            new Rect(currentStart, 0f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize)
                .CenteredOnYIn(rect), Color.white,
            Color.grey, NumQueued.ToString());
    }

    public static void DrawLabelForVanillaWindow(Rect rect, ResearchProjectDef projectToStart)
    {
        if (projectToStart.IsHidden)
        {
            return;
        }

        var researchNode = projectToStart.ResearchNode();
        if (!IsQueued(researchNode))
        {
            return;
        }

        var indexOf = researchNode.Research.IsAnomalyResearch() ? 
            _instance._anomalyQueue[projectToStart.knowledgeCategory].IndexOf(researchNode) : _instance._queue.IndexOf(researchNode);

        DrawLabel(
            new Rect(
                rect.xMax - 10f,
                rect.yMin + ((rect.height - Constants.SmallQueueLabelSize) / 2f),
                Constants.SmallQueueLabelSize,
                Constants.SmallQueueLabelSize),
            Color.white,
            Color.grey, indexOf + 1 + "");
    }

    private static void DrawLabel(Rect canvas, Color main, Color background, string label)
    {
        DrawLabel(canvas, main, background, label, string.Empty);
    }

    private static void DrawLabel(Rect canvas, Color main, Color background, string label, string tooltip)
    {
        FastGUI.DrawTextureFast(canvas, Assets.CircleFill, main);
        if (background != main)
        {
            FastGUI.DrawTextureFast(canvas.ContractedBy(2f), Assets.CircleFill, background);
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(canvas, label);
        if (!string.IsNullOrEmpty(tooltip))
        {
            TooltipHandler.TipRegion(canvas, tooltip);
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    public static void DrawQueue(Rect canvas, bool interactable)
    {
        if (!_instance._queue.Any())
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Assets.TechLevelColor;
            Widgets.Label(canvas, "Fluffy.ResearchTree.NothingQueued".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        var scrollContentRect = canvas;
        scrollContentRect.width = _instance._queue.Count * (Constants.NodeSize.x + Constants.Margin);
        scrollContentRect.height -= 20;
        scrollContentRect.x = 0;
        scrollContentRect.y = 0;

        Widgets.BeginScrollView(canvas, ref _sideScrollPosition, scrollContentRect);
        HandleMouseDown();
        var min = scrollContentRect.min;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < _instance._queue.Count; index++)
        {
            var node = _instance._queue[index];
            if (node == _draggedNode)
            {
                continue;
            }

            var rect = new Rect(
                min.x - Constants.Margin,
                min.y - Constants.Margin,
                Constants.NodeSize.x + (2 * Constants.Margin),
                Constants.NodeSize.y + (2 * Constants.Margin)
            );
            node.QueueRect = rect;
            node.DrawAt(min, rect, true);
            if (interactable && Mouse.IsOver(rect) && _draggedNode == null &&
                MainTabWindow_ResearchTree.Instance.IsQuickSearchWidgetEmpty())
            {
                MainTabWindow_ResearchTree.Instance.CenterOn(node);
            }

            min.x += Constants.NodeSize.x + Constants.Margin;
        }

        HandleDragging();
        HandleMouseUp();
        Widgets.EndScrollView();
    }

    public static void Notify_InstantFinished(ResearchNode node)
    {
        foreach (var item in new List<ResearchNode>(_instance._queue)
                     .Where(item => item.Research.IsFinished))
        {
            TryDequeue(item);
        }

        DoFinishResearchProject(node.Research);
    }

    public static void RefreshNode()
    {
        for (var i = 0; i < _instance._queue.Count; i++)
        {
            if (Tree.Instance.ResearchToNodesCache.TryGetValue(_instance._queue[i].Research, out var newNode))
            {
                _instance._queue[i] = newNode as ResearchNode;
            }
        }

        foreach (var knowledgeCategoryDef in _instance._anomalyQueue.Keys)
        {
            for (var i = 0; i < _instance._anomalyQueue[knowledgeCategoryDef].Count; i++)
            {
                if (Tree.Instance.ResearchToNodesCache.TryGetValue(_instance._anomalyQueue[knowledgeCategoryDef][i].Research, out var newNode))
                {
                    _instance._anomalyQueue[knowledgeCategoryDef][i] = newNode as ResearchNode;
                }
            }
        }

        if (_instance._saveableQueue.NullOrEmpty())
        {
            return;
        }
        
        foreach (var researchNode in _instance._saveableQueue.Select(item => item.ResearchNode())
                     .Where(researchNode => researchNode != null))
        {
            Enqueue(researchNode);
        }
        _instance._saveableQueue.Clear();
    }

    private static void AttemptBeginResearch()
    {
        AttemptBeginResearch(_instance._queue);
        _instance._anomalyQueue.Values.Do(anomalyQueue => AttemptBeginResearch(anomalyQueue, _instance._queue.Count <= 0));
    }

    private static void AttemptBeginResearch(List<ResearchNode> researchNodeQueue, bool needSelect = true)
    {
        var node = researchNodeQueue.FirstOrDefault();
        var projectToStart = node?.Research;
        if (projectToStart is not { CanStartNow: true } || projectToStart.IsFinished || Find.ResearchManager.IsCurrentProject(projectToStart))
        {
            return;
        }

        // to begin
        AttemptBeginResearchMethodInfo.Invoke(MainTabWindowResearchInstance, [projectToStart]);
        FocusStartedProject(projectToStart, needSelect);
    }

    private static void FocusStartedProject(ResearchProjectDef projectToStart, bool needSelect = true)
    {
        // focus the start project 
        Find.ResearchManager.SetCurrentProject(projectToStart);
        if (needSelect)
        {
            MainTabWindowResearchInstance.Select(projectToStart);
        }
    }

    private static void DoFinishResearchProject(ResearchProjectDef projectToFinish)
    {
        if (projectToFinish == null)
        {
            return;
        }

        // just FinishProject. next will execute TryStartNext.
        Find.ResearchManager.FinishProject(projectToFinish);
    }

    public static Dialog_MessageBox CreateConfirmation(ResearchProjectDef project,
        TaggedString text,
        Action confirmedAct,
        bool destructive = false,
        string title = null,
        WindowLayer layer = WindowLayer.Dialog)
    {
        return Dialog_MessageBox.CreateConfirmation(text, confirmedAct,
            () => TryDequeue(project.ResearchNode()), destructive, title, layer);
    }

    private static void TryToMove(ResearchNode researchNode)
    {
        if (researchNode == null || !IsQueued(researchNode))
        {
            return;
        }

        var current = _instance._queue.FirstOrDefault();
        var dropPosition = Event.current.mousePosition;
        var node = _instance._queue
            .OrderBy(item => Mathf.Abs(item.QueueRect.center.x - dropPosition.x))
            .First();

        var index = _instance._queue.IndexOf(node);
        var nodeCenterX = node.QueueRect.center.x;
        var queueCount = _instance._queue.Count;
        index = dropPosition.x <= nodeCenterX ? Mathf.Max(0, index) : Mathf.Min(index + 1, queueCount);
        var originIndex = _instance._queue.IndexOf(researchNode);
        if (index - 1 == originIndex)
        {
            // a magic code. Used to prevent subsequent left click events(ResearchNode.cs#L529-L539).
            // Maybe there are other ways that I don't know about, maybe the code that handles dragging needs to be implemented in another way?
            Event.current.button = -1;
            return;
        }

        if (index == queueCount)
        {
            _instance._queue.Add(researchNode);
        }
        else
        {
            _instance._queue.Insert(index, researchNode);
        }

        if (index < originIndex)
        {
            _instance._queue.RemoveAt(originIndex + 1);
        }
        else
        {
            _instance._queue.RemoveAt(originIndex);
        }

        SortRequiredRecursive(researchNode);
        var researchProjectDefList = researchNode.Research.Descendants();
        if (!researchProjectDefList.NullOrEmpty())
        {
            foreach (var research in researchProjectDefList
                         .Where(def => !def.IsFinished && IsQueued(def.ResearchNode())).ToList())
            {
                SortRequiredRecursive(research.ResearchNode());
            }
        }

        UpdateNodeQueueRect();

        var insertedIndex = _instance._queue.IndexOf(researchNode);
        var newCurrent = _instance._queue.FirstOrDefault();

        if (current != newCurrent && Input.GetMouseButtonUp(0) && insertedIndex != originIndex)
        {
            AttemptBeginResearch();
        }

        // Same as above
        Event.current.button = -1;
    }

    private static void SortRequiredRecursive(ResearchNode researchNode)
    {
        var index = _instance._queue.IndexOf(researchNode);
        foreach (var research in researchNode.GetMissingRequiredRecursive()
                     .Where(research => IsQueued(research) && _instance._queue.IndexOf(research) > index))
        {
            _instance._queue.Remove(research);
            _instance._queue.Insert(index, research);
            SortRequiredRecursive(research);
        }
    }

    private static void UpdateNodeQueueRect()
    {
        var vector2 = new Vector2(Constants.Margin, Constants.Margin);
        foreach (var researchNode in _instance._queue)
        {
            var rect = new Rect(vector2.x - Constants.Margin,
                vector2.y - Constants.Margin,
                Constants.NodeSize.x + (2 * Constants.Margin),
                Constants.NodeSize.y + (2 * Constants.Margin));
            researchNode.QueueRect = rect;

            vector2.x += Constants.NodeSize.x + Constants.Margin;
        }
    }

    private static void HandleMouseDown()
    {
        if (Event.current.type != EventType.MouseDown || Event.current.button != 0
                                                      || Event.current.control || Event.current.shift)
        {
            return;
        }

        _draggedNode = Enumerable.FirstOrDefault(_instance._queue,
            node => node.QueueRect.Contains(Event.current.mousePosition));
    }

    private static void HandleDragging()
    {
        if (_draggedNode == null)
        {
            return;
        }

        var position = Event.current.mousePosition;
        var size = new Vector2(
            Constants.NodeSize.x + (2 * Constants.Margin),
            Constants.NodeSize.y + (2 * Constants.Margin)
        );
        var rect = new Rect(position, size);
        _draggedNode.QueueRect = rect;
        _draggedNode.DrawAt(position, rect, true);
        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        TryToMove(_draggedNode);
    }

    private static void HandleMouseUp()
    {
        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        _draggedNode = null;
    }
}