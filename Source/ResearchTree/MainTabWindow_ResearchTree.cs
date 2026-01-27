// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class MainTabWindow_ResearchTree : MainTabWindow
{
    private static Vector2 _scrollPosition = Vector2.zero;

    private readonly HashSet<ResearchProjectDef> _matchingProjects = [];

    private readonly QuickSearchWidget _quickSearchWidget = new();

    private Rect _baseViewRect;

    private Rect _baseViewRectInner;

    private Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>
        _cachedUnlockedDefsGroupedByPrerequisites;

    private List<ResearchProjectDef> _cachedVisibleResearchProjects;

    private bool _dragging;

    private Vector2 _mousePosition = Vector2.zero;

    private Rect _viewRect;

    public bool ViewRectDirty = true;

    public bool ViewRectInnerDirty = true;

    public MainTabWindow_ResearchTree()
    {
        doWindowBackground = Assets.UsingMinimap;
        closeOnClickedOutside = false;
        Instance = this;
        preventCameraMotion = true;
        forcePause = FluffyResearchTreeMod.instance.Settings.PauseOnOpen;
    }

    public static MainTabWindow_ResearchTree Instance { get; private set; }

    public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

    public float ZoomLevel
    {
        get;
        private set
        {
            field = Mathf.Clamp(value, 1f, MaxZoomLevel);
            ViewRectDirty = true;
            ViewRectInnerDirty = true;
        }
    } = 1f;

    private Rect ViewRect
    {
        get
        {
            if (!ViewRectDirty)
            {
                return _viewRect;
            }

            _viewRect = new Rect(
                _baseViewRect.xMin * ZoomLevel,
                _baseViewRect.yMin * ZoomLevel,
                _baseViewRect.width * ZoomLevel,
                _baseViewRect.height * ZoomLevel);
            ViewRectDirty = false;

            return _viewRect;
        }
    }

    private Rect ViewRect_Inner
    {
        get
        {
            if (!ViewRectInnerDirty)
            {
                return field;
            }

            field = _viewRect.ContractedBy(Margin * ZoomLevel);
            ViewRectInnerDirty = false;

            return field;
        }
    }

    private static Rect TreeRect
    {
        get
        {
            if (field != default)
            {
                return field;
            }

            var width = Tree.Size.x * (Constants.NodeSize.x + Constants.NodeMargins.x);
            var height = Tree.Size.z * (Constants.NodeSize.y + Constants.NodeMargins.y) * 1.02f; // To avoid cutoff
            field = new Rect(0f, 0f, width, height);

            return field;
        }
    }

    private Rect VisibleRect => new(_scrollPosition.x, _scrollPosition.y, ViewRect_Inner.width, ViewRect_Inner.height);

    private float MaxZoomLevel
    {
        get
        {
            // get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
            var fitZoomLevel = Mathf.Max(TreeRect.width / _baseViewRectInner.width,
                TreeRect.height / _baseViewRectInner.height);
            return Mathf.Min(fitZoomLevel, Constants.AbsoluteMaxZoomLevel);
        }
    }

    private List<ResearchProjectDef> VisibleResearchProjects
    {
        get
        {
            return _cachedVisibleResearchProjects ??=
            [
                ..DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(d => Find.Storyteller.difficulty.AllowedBy(d.hideWhen) ||
                                Find.ResearchManager.IsCurrentProject(d))
            ];
        }
    }

    public void Notify_TreeInitialized()
    {
        setRects();
    }

    public override void PreOpen()
    {
        base.PreOpen();
        setRects();
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

        if (Assets.SemiRandomResearchLoaded)
        {
            var preValue = Assets.SemiResearchEnabled;
            Assets.SemiResearchEnabled = (bool)AccessTools
                .Field("CM_Semi_Random_Research.SemiRandomResearchModSettings:featureEnabled")
                .GetValue(Assets.SettingsInstance);
            if (preValue != Assets.SemiResearchEnabled)
            {
                Tree.ResetNodeAvailabilityCache();
            }
        }

        Queue.RefreshQueue();
        _dragging = false;
        closeOnClickedOutside = false;

        _cachedUnlockedDefsGroupedByPrerequisites = null;
        _cachedVisibleResearchProjects = null;
        _quickSearchWidget.Reset();
        updateSearchResults();
    }

    public override void WindowOnGUI()
    {
        base.WindowOnGUI();
        Assets.DrawWindowBackground(windowRect, FluffyResearchTreeMod.instance.Settings.BackgroundColor);
    }

    private void setRects()
    {
        var startPosition = new Vector2(StandardMargin / Prefs.UIScale,
            Constants.TopBarHeight + Constants.Margin + (StandardMargin / Prefs.UIScale));
        var size = new Vector2((Screen.width - (StandardMargin * 2f)) / Prefs.UIScale,
            UI.screenHeight - MainButtonDef.ButtonHeight - startPosition.y);

        _baseViewRect = new Rect(startPosition, size);
        _baseViewRectInner = _baseViewRect.ContractedBy(Constants.Margin / Prefs.UIScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;
    }

    public override void DoWindowContents(Rect canvas)
    {
        if (!Tree.Initialized)
        {
            Close();
            return;
        }

        drawTopBar(new Rect(canvas.xMin, canvas.yMin, canvas.width, Constants.TopBarHeight));
        applyZoomLevel();
        _scrollPosition = GUI.BeginScrollView(ViewRect, _scrollPosition, TreeRect);
        Tree.Draw(VisibleRect);
        Queue.DrawLabels(VisibleRect);
        handleZoom();
        GUI.EndScrollView(false);
        handleDragging();
        handleDolly();
        ResetZoomLevel();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }


    public override void Notify_ClickOutsideWindow()
    {
        base.Notify_ClickOutsideWindow();
        _quickSearchWidget.Unfocus();
    }

    // default W A S D move
    private static void handleDolly()
    {
        const float num = 10f;
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

    private void handleZoom()
    {
        if (!Event.current.isScrollWheel)
        {
            return;
        }

        if (Event.current.shift)
        {
            _scrollPosition.x += Event.current.delta.x * (FluffyResearchTreeMod.instance.Settings.ScrollSpeed * 10);
            return;
        }

        if (FluffyResearchTreeMod.instance.Settings.CtrlFunction == Event.current.control)
        {
            _scrollPosition.y += Event.current.delta.y * (FluffyResearchTreeMod.instance.Settings.ScrollSpeed * 10);
            return;
        }

        var mousePosition = Event.current.mousePosition;
        var vector = (Event.current.mousePosition - _scrollPosition) / ZoomLevel;
        ZoomLevel += Event.current.delta.y * Constants.ZoomStep * ZoomLevel;
        _scrollPosition = mousePosition - (vector * ZoomLevel);
        Event.current.Use();
    }

    private void handleDragging()
    {
        if (Queue._draggedNode != null)
        {
            return;
        }

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

    private void applyZoomLevel()
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
        GUI.BeginClip(new Rect(0f, UI.screenHeight - Constants.TopBarHeight,
            UI.screenWidth, UI.screenHeight - MainButtonDef.ButtonHeight - Constants.TopBarHeight));
    }

    private void drawTopBar(Rect canvas)
    {
        var rect = canvas;
        var rect2 = canvas;
        rect.width = 200f;
        rect2.xMin += 206f;
        DrawSearchBar(rect.ContractedBy(Constants.Margin));
        Queue.DrawQueue(rect2.ContractedBy(Constants.Margin), !_dragging);
    }

    private void DrawSearchBar(Rect canvas)
    {
        var searchRect =
            new Rect(canvas.xMin, 0f, canvas.width, Constants.QueueLabelSize).CenteredOnYIn(canvas.TopHalf());

        var anomalyBtnRect = new Rect(
            searchRect.x + Constants.SmallQueueLabelSize + Constants.Margin,
            searchRect.y,
            searchRect.width - Constants.SmallQueueLabelSize - Constants.Margin,
            searchRect.height
        ).CenteredOnYIn(canvas.BottomHalf());
        if (ModsConfig.AnomalyActive && Widgets.ButtonText(anomalyBtnRect, "Fluffy.ResearchTree.Anomaly".Translate()))
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Anomaly;
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
            return;
        }

        _quickSearchWidget.OnGUI(searchRect, () => updateSearchResults(canvas));
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

    public bool IsHighlighted(ResearchProjectDef research)
    {
        return IsQuickSearchWidgetActive() && _matchingProjects.Contains(research);
    }

    public bool IsQuickSearchWidgetActive()
    {
        return _quickSearchWidget.filter.Active;
    }

    public bool IsQuickSearchWidgetEmpty()
    {
        return string.IsNullOrEmpty(_quickSearchWidget.filter.Text);
    }

    private void updateSearchResults(Rect searchRect = default)
    {
        _quickSearchWidget.noResultsMatched = false;
        _matchingProjects.Clear();
        Find.WindowStack.FloatMenu?.Close(false);

        if (!IsQuickSearchWidgetActive())
        {
            return;
        }

        foreach (var researchProject in VisibleResearchProjects.Where(researchProject => !researchProject.IsHidden &&
                     (_quickSearchWidget.filter.Matches(researchProject.LabelCap) ||
                      matchesUnlockedDefs(researchProject))))
        {
            _matchingProjects.Add(researchProject);
        }

        _quickSearchWidget.noResultsMatched = !_matchingProjects.Any();

        var somethingHighlighted = true;
        var list = new List<FloatMenuOption>();
        foreach (var node in Tree.Nodes.OfType<ResearchNode>()
                     .Where(n => _matchingProjects.Contains(n.Research))
                     .OrderBy(n => n.Research.ResearchViewX))
        {
            list.Add(new FloatMenuOption(
                node.Label,
                delegate
                {
                    _quickSearchWidget.filter.Text = node.Label;
                    CenterOn(node);
                },
                MenuOptionPriority.Default,
                delegate
                {
                    somethingHighlighted = false;
                    _matchingProjects.Clear();
                    _matchingProjects.Add(node.Research);
                },
                playSelectionSound: false)
            );
            node.Highlighted = true;
            if (!somethingHighlighted)
            {
                continue;
            }

            //CenterOn(node);
            somethingHighlighted = false;
        }

        if (!_quickSearchWidget.CurrentlyFocused() || !list.Any())
        {
            return;
        }

        searchRect.x += QuickSearchWidget.IconSize;
        Find.WindowStack.Add(new FloatMenu_Fixed(searchRect, list));

        return;

        bool matchesUnlockedDefs(ResearchProjectDef proj)
        {
            return unlockedDefsGroupedByPrerequisites(proj)
                .SelectMany(groupedByPrerequisite => groupedByPrerequisite.Second)
                .Any(MatchesUnlockedDef);
        }
    }

    public bool MatchesUnlockedDef(Def unlocked)
    {
        return _quickSearchWidget.filter.Matches(unlocked.label);
    }

    private List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> unlockedDefsGroupedByPrerequisites(
        ResearchProjectDef project)
    {
        _cachedUnlockedDefsGroupedByPrerequisites ??=
            new Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>();
        if (_cachedUnlockedDefsGroupedByPrerequisites.TryGetValue(project, out var pairList))
        {
            return pairList;
        }

        pairList = ResearchPrerequisitesUtility.UnlockedDefsGroupedByPrerequisites(project);
        _cachedUnlockedDefsGroupedByPrerequisites.Add(project, pairList);
        return pairList;
    }
}