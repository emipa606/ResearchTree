// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class Queue : WorldComponent
{
    public static Queue Instance;

    private static Vector2 _sideScrollPosition = Vector2.zero;

    private static readonly MainTabWindow_Research MainTabWindowResearchInstance =
        (MainTabWindow_Research)MainButtonDefOf.Research.TabWindow;

    public static ResearchNode _draggedNode;

    private readonly List<ResearchNode> _queue = [];

    private List<ResearchProjectDef> _saveableQueue;

    public Queue(World world) : base(world)
    {
        Instance = this;
    }

    public void Clear()
    {
        _queue.Clear();
    }

    public static ResearchNode Pop
    {
        get
        {
            if (Instance._queue is not { Count: > 0 })
            {
                return null;
            }

            var result = Instance._queue[0];
            Instance._queue.RemoveAt(0);
            return result;
        }
    }

    public static int NumQueued => Instance._queue.Count;

    public static void TryDequeue(ResearchNode node)
    {
        if (Instance._queue.Contains(node))
        {
            Dequeue(node);
        }
    }

    private static void Dequeue(ResearchNode node)
    {
        var removeFirst = false;
        var indexOf = Instance._queue.IndexOf(node);
        if (indexOf >= 0)
        {
            Instance._queue.RemoveAt(indexOf);
            removeFirst = indexOf == 0;
        }

        node.QueueRect = Rect.zero;
        foreach (var item in Instance._queue.Where(n => n.GetMissingRequiredRecursive().Contains(node)).ToList())
        {
            indexOf = Instance._queue.IndexOf(item);
            if (indexOf < 0)
            {
                continue;
            }

            item.QueueRect = Rect.zero;
            Instance._queue.RemoveAt(indexOf);
            if (!removeFirst && indexOf == 0)
            {
                removeFirst = true;
            }
        }

        if (Find.ResearchManager.currentProj == node.Research)
        {
            Find.ResearchManager.currentProj = null;
        }

        // try to remove duplicate confirmation window
        Find.WindowStack.TryRemoveAssignableFromType(typeof(Dialog_MessageBox), false);

        if (removeFirst)
        {
            AttemptBeginResearch();
        }
    }

    private static void Enqueue(ResearchNode node, bool add)
    {
        if (node.Research.IsAnomalyResearch())
        {
            return;
        }

        if (!add)
        {
            Instance._queue.Clear();
            Find.ResearchManager.currentProj = null;
        }

        if (!Instance._queue.Contains(node))
        {
            Instance._queue.Add(node);
        }
    }

    private static void ReEnqueue(ResearchNode node)
    {
        if (node.Research.IsAnomalyResearch())
        {
            return;
        }

        if (!Instance._queue.Contains(node))
        {
            Instance._queue.Insert(0, node);
            return;
        }

        var index = Instance._queue.IndexOf(node);
        for (var i = index; i > 0; i--)
        {
            Instance._queue[i] = Instance._queue[i - 1];
        }

        Instance._queue[0] = node;
    }

    public static void EnqueueRangeFirst(IEnumerable<ResearchNode> nodes)
    {
        var researchOrder = nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent).ToList();

        if (IsEnqueueRangeFirstSameOrder(researchOrder))
        {
            return;
        }

        var current = Instance._queue.FirstOrDefault();
        researchOrder.Reverse();
        foreach (var item in researchOrder)
        {
            ReEnqueue(item);
        }

        if (current != Instance._queue.FirstOrDefault())
        {
            AttemptBeginResearch();
        }

        UpdateNodeQueueRect();
    }

    public static void EnqueueFirst(IEnumerable<ResearchNode> nodes)
    {
        var researchOrder = nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent).ToList();

        if (IsEnqueueRangeFirstSameOrder(researchOrder))
        {
            return;
        }

        researchOrder.Reverse();
        foreach (var item in researchOrder)
        {
            ReEnqueue(item);
        }

        FocusStartedProject(researchOrder.LastOrDefault()?.Research);
        UpdateNodeQueueRect();
    }

    public static bool IsEnqueueRangeFirstSameOrder(IEnumerable<ResearchNode> nodes,
        bool nodesOrdered = true, bool warning = true)
    {
        if (nodes == null)
        {
            return false;
        }

        var researchOrder = !nodesOrdered
            ? nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent).ToList()
            : nodes.ToList();

        if (researchOrder.Count > Instance._queue.Count)
        {
            return false;
        }

        var sameOrder = !researchOrder.Where((t, i) => t != Instance._queue[i]).Any();

        if (!sameOrder)
        {
            return false;
        }

        if (warning)
        {
            Messages.Message("Fluffy.ResearchTree.CannotMoveMore".Translate(researchOrder.Last().Label), null,
                MessageTypeDefOf.RejectInput);
        }

        return true;
    }

    public static void EnqueueRange(IEnumerable<ResearchNode> nodes, bool add)
    {
        if (!add)
        {
            Instance._queue.Clear();
            Find.ResearchManager.currentProj = null;
        }

        var firstEnqueue = Instance._queue.Empty();
        foreach (var item in nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent))
        {
            Enqueue(item, true);
        }

        if (firstEnqueue)
        {
            AttemptBeginResearch();
        }

        UpdateNodeQueueRect();
    }

    public static bool IsQueued(ResearchNode node)
    {
        return Instance._queue.Contains(node) && !node.Research.IsAnomalyResearch();
    }

    public static void TryStartNext(ResearchProjectDef finished)
    {
        if (!IsQueued(finished.ResearchNode()))
        {
            // Filtered unlocked research that comes with the start
            return;
        }

        TryDequeue(finished.ResearchNode());
        var current = Instance._queue.FirstOrDefault();
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
        if (next != null)
        {
            text2 += "\n\n" + "Fluffy.ResearchTree.NextInQueue".Translate(next.LabelCap);
            Find.LetterStack.ReceiveLetter(text, text2, LetterDefOf.PositiveEvent);
        }
        else
        {
            text2 += "\n\n" + "Fluffy.ResearchTree.NextInQueue".Translate("Fluffy.ResearchTree.None".Translate());
            Find.LetterStack.ReceiveLetter(text, text2, LetterDefOf.NeutralEvent);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _saveableQueue = _queue.Select(node => node.Research).ToList();
        }

        Scribe_Collections.Look(ref _saveableQueue, "Queue", LookMode.Def);
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        foreach (var researchNode in _saveableQueue.Select(item => item.ResearchNode())
                     .Where(researchNode => researchNode != null))
        {
            Enqueue(researchNode, true);
        }
    }

    public static void DrawLabels(Rect visibleRect)
    {
        var num = 1;
        foreach (var item in Instance._queue)
        {
            var rect = new Rect(item.Rect.xMax - (Constants.QueueLabelSize / 2f),
                item.Rect.yMin + ((item.Rect.height - Constants.QueueLabelSize) / 2f),
                Constants.QueueLabelSize,
                Constants.QueueLabelSize);
            if (item.IsVisible(visibleRect))
            {
                var color = Assets.ColorCompleted[item.Research.techLevel];
                var background = num > 1 ? Assets.ColorUnavailable[item.Research.techLevel] : color;
                DrawLabel(rect, color, background, num.ToString());
            }

            num++;
        }
    }

    public static void DrawLabelForMainButton(Rect rect)
    {
        var currentStart = rect.xMax - Constants.SmallQueueLabelSize - Constants.Margin;
        if (!Tree.Initialized && FluffyResearchTreeMod.instance.Settings.LoadType != Constants.LoadTypeFirstTimeOpening
                              && FluffyResearchTreeMod.instance.Settings.OverrideResearch)
        {
            DrawLabel(
                new Rect(currentStart, 0f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize)
                    .CenteredOnYIn(rect), Color.yellow,
                Color.grey, "..", "Fluffy.ResearchTree.StillLoading".Translate());
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
        if (projectToStart.IsAnomalyResearch())
        {
            return;
        }

        var researchNode = projectToStart.ResearchNode();
        if (!IsQueued(researchNode))
        {
            return;
        }

        DrawLabel(
            new Rect(
                rect.xMax - 10f,
                rect.yMin + ((rect.height - Constants.SmallQueueLabelSize) / 2f),
                Constants.SmallQueueLabelSize,
                Constants.SmallQueueLabelSize),
            Color.white,
            Color.grey, Instance._queue.IndexOf(researchNode) + 1 + "");
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

    public static void DrawQueue(Rect canvas, bool interactible)
    {
        if (!Instance._queue.Any())
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Assets.TechLevelColor;
            Widgets.Label(canvas, "Fluffy.ResearchTree.NothingQueued".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        var scrollContentRect = canvas;
        scrollContentRect.width = Instance._queue.Count * (Constants.NodeSize.x + Constants.Margin);
        scrollContentRect.height -= 20;
        scrollContentRect.x = 0;
        scrollContentRect.y = 0;

        Widgets.BeginScrollView(canvas, ref _sideScrollPosition, scrollContentRect);
        HandleMouseDown();
        var min = scrollContentRect.min;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < Instance._queue.Count; index++)
        {
            var node = Instance._queue[index];
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
            if (interactible && Mouse.IsOver(rect) && _draggedNode == null &&
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
        foreach (var item in new List<ResearchNode>(Instance._queue)
                     .Where(item => item.Research.IsFinished))
        {
            TryDequeue(item);
        }

        DoFinishResearchProject(node.Research);
    }

    public static void RefreshQueue()
    {
        if (Find.ResearchManager.currentProj == null)
        {
            return;
        }

        if (!Instance._queue.Any())
        {
            Enqueue(Find.ResearchManager.currentProj.ResearchNode(), true);
        }
    }

    private static void AttemptBeginResearch()
    {
        var node = Instance._queue.FirstOrDefault();
        var projectToStart = node?.Research;
        if (projectToStart is not { CanStartNow: true } || projectToStart.IsFinished)
        {
            return;
        }

        // to begin
        MainTabWindowResearchInstance.AttemptBeginResearch(projectToStart);
        FocusStartedProject(projectToStart);
    }

    private static void FocusStartedProject(ResearchProjectDef projectToStart)
    {
        // focus the start project 
        Find.ResearchManager.SetCurrentProject(projectToStart);
        MainTabWindowResearchInstance.Select(projectToStart);
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

        var current = Instance._queue.FirstOrDefault();
        var dropPosition = Event.current.mousePosition;
        var node = Instance._queue
            .OrderBy(item => Mathf.Abs(item.QueueRect.center.x - dropPosition.x))
            .First();

        var index = Instance._queue.IndexOf(node);
        var nodeCenterX = node.QueueRect.center.x;
        var queueCount = Instance._queue.Count;
        index = dropPosition.x <= nodeCenterX ? Mathf.Max(0, index) : Mathf.Min(index + 1, queueCount);
        var originIndex = Instance._queue.IndexOf(researchNode);
        if (index - 1 == originIndex)
        {
            // a magic code. Used to prevent subsequent left click events(ResearchNode.cs#L529-L539).
            // Maybe there are other ways that I don't know about, maybe the code that handles dragging needs to be implemented in another way?
            Event.current.button = -1;
            return;
        }

        if (index == queueCount)
        {
            Instance._queue.Add(researchNode);
        }
        else
        {
            Instance._queue.Insert(index, researchNode);
        }

        if (index < originIndex)
        {
            Instance._queue.RemoveAt(originIndex + 1);
        }
        else
        {
            Instance._queue.RemoveAt(originIndex);
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

        var insertedIndex = Instance._queue.IndexOf(researchNode);
        var newCurrent = Instance._queue.FirstOrDefault();

        if (current != newCurrent && Input.GetMouseButtonUp(0) && insertedIndex != originIndex)
        {
            AttemptBeginResearch();
        }

        // Same as above
        Event.current.button = -1;
    }

    private static void SortRequiredRecursive(ResearchNode researchNode)
    {
        var index = Instance._queue.IndexOf(researchNode);
        foreach (var research in researchNode.GetMissingRequiredRecursive()
                     .Where(research => IsQueued(research) && Instance._queue.IndexOf(research) > index))
        {
            Instance._queue.Remove(research);
            Instance._queue.Insert(index, research);
            SortRequiredRecursive(research);
        }
    }

    private static void UpdateNodeQueueRect()
    {
        var vector2 = new Vector2(Constants.Margin, Constants.Margin);
        foreach (var researchNode in Instance._queue)
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

        _draggedNode = Enumerable.FirstOrDefault(Instance._queue,
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