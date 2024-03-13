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

    public bool _viewRect_InnerDirty = true;

    public bool _viewRectDirty = true;

    private float _zoomLevel = 1f;

    public MainTabWindow_ResearchTree()
    {
        closeOnClickedOutside = false;
        Instance = this;
        preventCameraMotion = true;
        forcePause = FluffyResearchTreeMod.instance.Settings.PauseOnOpen;
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

            _viewRect = new Rect(_baseViewRect.xMin * ZoomLevel,
                _baseViewRect.yMin * ZoomLevel,
                _baseViewRect.width * ZoomLevel,
                _baseViewRect.height * ZoomLevel);
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
            var height = Tree.Size.z * (Constants.NodeSize.y + Constants.NodeMargins.y) * 1.02f; // To avoid cutoff
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
        Tree.WaitForInitialization();
        Assets.RefreshResearch = true;
        if (Tree.FirstLoadDone)
        {
            Tree.ResetNodeAvailabilityCache();
        }
        else
        {
            Tree.FirstLoadDone = true;
        }

        Queue.RefreshQueue();
        _dragging = false;
        closeOnClickedOutside = false;
    }

    private void SetRects()
    {
        var startPosition = new Vector2(18f / Prefs.UIScale,
            Constants.TopBarHeight + Constants.Margin + (18f / Prefs.UIScale));
        var size = new Vector2((Screen.width - 36f) / Prefs.UIScale,
            UI.screenHeight - 35f - startPosition.y);

        _baseViewRect = new Rect(startPosition, size);
        _baseViewRect_Inner = _baseViewRect.ContractedBy(Constants.Margin / Prefs.UIScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        windowRect.height = UI.screenHeight - (35f / Prefs.UIScale) - (Constants.Margin / Prefs.UIScale);
        Log.Message($"screen: {Screen.width}x{Screen.height}");
        Log.Message($"_baseViewRect: {_baseViewRect.width}x{_baseViewRect.height}");
    }

    public override void DoWindowContents(Rect canvas)
    {
        if (!Tree.Initialized)
        {
            return;
        }

        DrawTopBar(new Rect(canvas.xMin, canvas.yMin + 10f, canvas.width, Constants.TopBarHeight));
        ApplyZoomLevel();
        FastGUI.DrawTextureFast(ViewRect, Assets.SlightlyDarkBackground);
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

        if (Event.current.control == FluffyResearchTreeMod.instance.Settings.CtrlFunction)
        {
            _scrollPosition.y += Event.current.delta.y * 10f;
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
            UI.UnfocusCurrentControl();
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

    public void ResetZoomLevel()
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
        FastGUI.DrawTextureFast(rect, Assets.SlightlyDarkBackground);
        FastGUI.DrawTextureFast(rect2, Assets.SlightlyDarkBackground);
        DrawSearchBar(rect.ContractedBy(Constants.Margin));
        Queue.DrawQueue(rect2.ContractedBy(Constants.Margin), !_dragging);
    }

    private void DrawSearchBar(Rect canvas)
    {
        var position =
            new Rect(canvas.xMax - Constants.Margin - Constants.HubSize, 0f, Constants.HubSize, Constants.HubSize)
                .CenteredOnYIn(canvas.TopHalf());
        var rect = new Rect(canvas.xMin, 0f, canvas.width, Constants.QueueLabelSize).CenteredOnYIn(canvas.TopHalf());
        FastGUI.DrawTextureFast(position, Assets.Search);
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
        foreach (var result2 in Tree.Nodes.OfType<ResearchNode>()
                     .Select(n => new { node = n, match = n.Matches(query) })
                     .Where(result => result.match > 0)
                     .OrderBy(result => result.match))
        {
            list.Add(new FloatMenuOption(result2.node.Label, delegate { },
                MenuOptionPriority.Default, delegate
                {
                    CenterOn(result2.node);
                }));
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