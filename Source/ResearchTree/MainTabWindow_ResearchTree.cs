// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class MainTabWindow_ResearchTree : MainTabWindow
{
    internal static Vector2 _scrollPosition = Vector2.zero;

    private static Rect _treeRect;

    private Rect _baseViewRect;

    private Rect _baseViewRect_Inner;

    private bool _dragging;

    private Vector2 _mousePosition = Vector2.zero;

    private string _query = "";

    private Rect _viewRect;

    private Rect _viewRect_Inner;

    private bool _viewRect_InnerDirty = true;

    private bool _viewRectDirty = true;

    private float _zoomLevel = 1f;

    public MainTabWindow_ResearchTree()
    {
        closeOnClickedOutside = false;
        Instance = this;
    }

    public static MainTabWindow_ResearchTree Instance { get; private set; }

    public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

    public float ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            _zoomLevel = Mathf.Clamp(value, 1f, MaxZoomLevel);
            _viewRectDirty = true;
            _viewRect_InnerDirty = true;
        }
    }

    public Rect ViewRect
    {
        get
        {
            if (!_viewRectDirty)
            {
                return _viewRect;
            }

            _viewRect = new Rect(_baseViewRect.xMin * ZoomLevel, _baseViewRect.yMin * ZoomLevel,
                _baseViewRect.width * ZoomLevel, _baseViewRect.height * ZoomLevel);
            _viewRectDirty = false;

            return _viewRect;
        }
    }

    public Rect ViewRect_Inner
    {
        get
        {
            if (!_viewRect_InnerDirty)
            {
                return _viewRect_Inner;
            }

            _viewRect_Inner = _viewRect.ContractedBy(Margin * ZoomLevel);
            _viewRect_InnerDirty = false;

            return _viewRect_Inner;
        }
    }

    public Rect TreeRect
    {
        get
        {
            if (_treeRect != default)
            {
                return _treeRect;
            }

            var width = Tree.Size.x * (Constants.NodeSize.x + Constants.NodeMargins.x);
            var height = Tree.Size.z * (Constants.NodeSize.y + Constants.NodeMargins.y);
            _treeRect = new Rect(0f, 0f, width, height);

            return _treeRect;
        }
    }

    public Rect VisibleRect =>
        new Rect(_scrollPosition.x, _scrollPosition.y, ViewRect_Inner.width, ViewRect_Inner.height);

    internal float MaxZoomLevel =>
        Mathf.Min(Mathf.Max(TreeRect.width / _baseViewRect_Inner.width, TreeRect.height / _baseViewRect_Inner.height),
            Constants.AbsoluteMaxZoomLevel);

    public void Notify_TreeInitialized()
    {
        SetRects();
    }

    public override void PreOpen()
    {
        base.PreOpen();
        SetRects();
        if (!Tree.Initialized)
        {
            Tree.Initialize();
        }

        ResearchNode.ClearCaches();
        _dragging = false;
        closeOnClickedOutside = false;
    }

    private void SetRects()
    {
        _baseViewRect = new Rect(18f / Prefs.UIScale, (Constants.TopBarHeight + Constants.Margin + 18f) / Prefs.UIScale,
            (Screen.width - 36f) / Prefs.UIScale,
            (Screen.height - 35 - 36f - Constants.TopBarHeight - Constants.Margin) / Prefs.UIScale);
        _baseViewRect_Inner = _baseViewRect.ContractedBy(Constants.Margin / Prefs.UIScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        windowRect.height = UI.screenHeight - 35;
    }

    public override void DoWindowContents(Rect canvas)
    {
        if (!Tree.Initialized)
        {
            return;
        }

        var canvas2 = new Rect(canvas.xMin, canvas.yMin, canvas.width, Constants.TopBarHeight);
        DrawTopBar(canvas2);
        ApplyZoomLevel();
        GUI.DrawTexture(ViewRect, Assets.SlightlyDarkBackground);
        _scrollPosition = GUI.BeginScrollView(ViewRect, _scrollPosition, TreeRect);
        GUI.BeginGroup(new Rect(ScaledMargin, ScaledMargin, TreeRect.width + (ScaledMargin * 2f),
            TreeRect.height + (ScaledMargin * 2f)));
        Tree.Draw(VisibleRect);
        Queue.DrawLabels(VisibleRect);
        HandleZoom();
        GUI.EndGroup();
        GUI.EndScrollView(false);
        HandleDragging();
        HandleDolly();
        ResetZoomLevel();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void HandleDolly()
    {
        var num = 10f;
        if (KeyBindingDefOf.MapDolly_Left.IsDown)
        {
            _scrollPosition.x -= num;
        }

        if (KeyBindingDefOf.MapDolly_Right.IsDown)
        {
            _scrollPosition.x += num;
        }

        if (KeyBindingDefOf.MapDolly_Up.IsDown)
        {
            _scrollPosition.y -= num;
        }

        if (KeyBindingDefOf.MapDolly_Down.IsDown)
        {
            _scrollPosition.y += num;
        }
    }

    private void HandleZoom()
    {
        if (!Event.current.isScrollWheel)
        {
            return;
        }

        var mousePosition = Event.current.mousePosition;
        var vector = (Event.current.mousePosition - _scrollPosition) / ZoomLevel;
        ZoomLevel += Event.current.delta.y * Constants.ZoomStep * ZoomLevel;
        _scrollPosition = mousePosition - (vector * ZoomLevel);
        Event.current.Use();
    }

    private void HandleDragging()
    {
        if (Event.current.type == EventType.MouseDown)
        {
            _dragging = true;
            _mousePosition = Event.current.mousePosition;
            Event.current.Use();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            _dragging = false;
            _mousePosition = Vector2.zero;
        }

        if (Event.current.type != EventType.MouseDrag)
        {
            return;
        }

        var mousePosition = Event.current.mousePosition;
        _scrollPosition += _mousePosition - mousePosition;
        _mousePosition = mousePosition;
    }

    private void ApplyZoomLevel()
    {
        GUI.EndClip();
        GUI.EndClip();
        GUI.matrix = Matrix4x4.TRS(new Vector3(0f, 0f, 0f), Quaternion.identity,
            new Vector3(Prefs.UIScale / ZoomLevel, Prefs.UIScale / ZoomLevel, 1f));
    }

    private void ResetZoomLevel()
    {
        UI.ApplyUIScale();
        GUI.BeginClip(windowRect);
        GUI.BeginClip(new Rect(0f, 0f, UI.screenWidth, UI.screenHeight));
    }

    private void DrawTopBar(Rect canvas)
    {
        var rect = canvas;
        var rect2 = canvas;
        rect.width = 200f;
        rect2.xMin += 206f;
        GUI.DrawTexture(rect, Assets.SlightlyDarkBackground);
        GUI.DrawTexture(rect2, Assets.SlightlyDarkBackground);
        DrawSearchBar(rect.ContractedBy(Constants.Margin));
        Queue.DrawQueue(rect2.ContractedBy(Constants.Margin), !_dragging);
    }

    private void DrawSearchBar(Rect canvas)
    {
        var position =
            new Rect(canvas.xMax - Constants.Margin - Constants.HubSize, 0f, Constants.HubSize, Constants.HubSize)
                .CenteredOnYIn(canvas);
        var rect = new Rect(canvas.xMin, 0f, canvas.width, Constants.QueueLabelSize).CenteredOnYIn(canvas);
        GUI.DrawTexture(position, Assets.Search);
        var query = Widgets.TextField(rect, _query);
        if (query == _query)
        {
            return;
        }

        _query = query;
        Find.WindowStack.FloatMenu?.Close(false);
        if (query.Length <= 2)
        {
            return;
        }

        var list = new List<FloatMenuOption>();
        foreach (var result2 in from n in Tree.Nodes.OfType<ResearchNode>()
                 select new
                 {
                     node = n,
                     match = n.Matches(query)
                 }
                 into result
                 where result.match > 0
                 orderby result.match
                 select result)
        {
            list.Add(new FloatMenuOption(result2.node.Label, delegate { CenterOn(result2.node); },
                MenuOptionPriority.Default, delegate { CenterOn(result2.node); }));
        }

        if (!list.Any())
        {
            list.Add(new FloatMenuOption("Fluffy.ResearchTree.NoResearchFound".Translate(), null));
        }

        Find.WindowStack.Add(new FloatMenu_Fixed(list, UI.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMax))));
    }

    public void CenterOn(Node node)
    {
        var scrollPosition = new Vector2((Constants.NodeSize.x + Constants.NodeMargins.x) * (node.X - 0.5f),
            (Constants.NodeSize.y + Constants.NodeMargins.y) * (node.Y - 0.5f));
        node.Highlighted = true;
        scrollPosition -= new Vector2(UI.screenWidth, UI.screenHeight) / 2f;
        scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0f, TreeRect.width - ViewRect.width);
        scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, TreeRect.height - ViewRect.height);
        _scrollPosition = scrollPosition;
    }
}