// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyResearchTree;

public class Queue : WorldComponent
{
    private static Queue _instance;

    private static Vector2 _sideScrollPosition = Vector2.zero;

    private static MethodInfo _doBeginResearchMethodInfo =
        AccessTools.Method(typeof(MainTabWindow_Research), "DoBeginResearch", [typeof(ResearchProjectDef)]);

    private static MainTabWindow_Research _mainTabWindow_ResearchInstance = 
        (MainTabWindow_Research)Assets.MainButtonDefOf.ResearchOriginal.TabWindow;

    private readonly List<ResearchNode> _queue = [];

    private List<ResearchProjectDef> _saveableQueue;

    public Queue(World world)
        : base(world)
    {
        _instance = this;
    }

    public static ResearchNode Pop
    {
        get
        {
            if (_instance._queue is not { Count: > 0 })
            {
                return null;
            }

            var result = _instance._queue[0];
            _instance._queue.RemoveAt(0);
            return result;
        }
    }

    public static int NumQueued => _instance._queue.Count - 1;

    public static void TryDequeue(ResearchNode node)
    {
        if (_instance._queue.Contains(node))
        {
            Dequeue(node);
        }
    }

    public static void Dequeue(ResearchNode node)
    {
        _instance._queue.Remove(node);
        foreach (var item in _instance._queue.Where(n => n.GetMissingRequiredRecursive().Contains(node)).ToList())
        {
            _instance._queue.Remove(item);
        }

        if (Find.ResearchManager.currentProj == node.Research)
        {
            Find.ResearchManager.currentProj = null;
        }
    }

    public static void DrawLabels(Rect visibleRect)
    {
        var num = 1;
        foreach (var item in _instance._queue)
        {
            if (item.IsVisible(visibleRect))
            {
                var color = Assets.ColorCompleted[item.Research.techLevel];
                var background = num > 1 ? Assets.ColorUnavailable[item.Research.techLevel] : color;
                DrawLabel(item.QueueRect, color, background, num.ToString());
            }

            num++;
        }
    }

    public static void DrawLabel(Rect canvas, Color main, Color background, string label)
    {
        DrawLabel(canvas, main, background, label, string.Empty);
    }

    public static void DrawLabel(Rect canvas, Color main, Color background, string label, string tooltip)
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

    public static void Enqueue(ResearchNode node, bool add)
    {
        if (!add)
        {
            _instance._queue.Clear();
            Find.ResearchManager.currentProj = null;
        }

        if (!_instance._queue.Contains(node))
        {
            _instance._queue.Add(node);
        }

        DoBeginResearch(_instance._queue.First()?.Research);
    }


    private static void Requeue(ResearchNode node)
    {
        if (!_instance._queue.Contains(node))
        {
            _instance._queue.Insert(0, node);
            return;
        }

        var index = _instance._queue.IndexOf(node);
        for (var i = index; i > 0; i--)
        {
            _instance._queue[i] = _instance._queue[i - 1];
        }

        _instance._queue[0] = node;
    }


    public static void EnqueueRangeFirst(IEnumerable<ResearchNode> nodes)
    {
        TutorSystem.Notify_Event("StartResearchProject");

        var researchOrder = nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent).ToList();

        if (researchOrder.Count() <= _instance._queue.Count())
        {
            var sameOrder = true;
            for (var i = 0; i < researchOrder.Count(); i++)
            {
                if (researchOrder[i] == _instance._queue[i])
                {
                    continue;
                }

                sameOrder = false;
                break;
            }

            if (sameOrder)
            {
                Messages.Message("Fluffy.ResearchTree.CannotMoveMore".Translate(researchOrder.Last().Label), null,
                    MessageTypeDefOf.RejectInput);
                return;
            }
        }

        researchOrder.Reverse();
        foreach (var item in researchOrder)
        {
            Requeue(item);
        }

        DoBeginResearch(researchOrder.Last()?.Research);
    }


    public static void EnqueueRange(IEnumerable<ResearchNode> nodes, bool add)
    {
        TutorSystem.Notify_Event("StartResearchProject");
        if (!add)
        {
            _instance._queue.Clear();
            Find.ResearchManager.currentProj = null;
        }

        foreach (var item in nodes.OrderBy(node => node.X).ThenBy(node => node.Research.CostApparent))
        {
            Enqueue(item, true);
        }
    }
    
    private static void DoBeginResearch(ResearchProjectDef projectToStart)
    {
        if (projectToStart == null)
        {
            return;
        }
        _doBeginResearchMethodInfo.Invoke(_mainTabWindow_ResearchInstance, [projectToStart]);
    }

    public static bool IsQueued(ResearchNode node)
    {
        return _instance._queue.Contains(node);
    }

    public static void TryStartNext(ResearchProjectDef finished)
    {
        var current = _instance._queue.FirstOrDefault()?.Research;
        if (finished != _instance._queue.FirstOrDefault()?.Research)
        {
            TryDequeue(finished.ResearchNode());
            return;
        }

        _instance._queue.RemoveAt(0);
        var researchProjectDef = _instance._queue.FirstOrDefault()?.Research;
        DoBeginResearch(researchProjectDef);
        DoCompletionLetter(current, researchProjectDef);
    }

    private static void DoCompletionLetter(ResearchProjectDef current, ResearchProjectDef next)
    {
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

        foreach (var item in _saveableQueue)
        {
            var researchNode = item.ResearchNode();
            if (researchNode != null)
            {
                Enqueue(researchNode, true);
            }
        }
    }

    public static void DrawQueue(Rect canvas, bool interactible)
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
        var min = scrollContentRect.min;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < _instance._queue.Count; index++)
        {
            var node = _instance._queue[index];

            var rect = new Rect(min.x - Constants.Margin, min.y - Constants.Margin,
                Constants.NodeSize.x + (Constants.Margin * 2),
                Constants.NodeSize.y + 12f);
            if (rect.height > scrollContentRect.height)
            {
                rect = new Rect(min.x - Constants.Margin, min.y - Constants.Margin,
                    Constants.NodeSize.x + (Constants.Margin * 2),
                    Math.Min(Constants.NodeSize.y + 12f, scrollContentRect.height));
                node.DrawAt(min, rect, false, true);
            }
            else
            {
                node.DrawAt(min, rect, true);
            }

            if (interactible && Mouse.IsOver(rect))
            {
                MainTabWindow_ResearchTree.Instance.CenterOn(node);
            }

            min.x += Constants.NodeSize.x + Constants.Margin;
        }

        Widgets.EndScrollView();
    }

    public static void Notify_InstantFinished()
    {
        foreach (var item in new List<ResearchNode>(_instance._queue))
        {
            if (item.Research.IsFinished)
            {
                TryDequeue(item);
            }
        }

        DoBeginResearch(_instance._queue.FirstOrDefault()?.Research);
    }

    public static void RefreshQueue()
    {
        if (Find.ResearchManager.currentProj == null)
        {
            return;
        }

        if (!_instance._queue.Any())
        {
            Enqueue(Find.ResearchManager.currentProj.ResearchNode(), true);
        }
    }
}