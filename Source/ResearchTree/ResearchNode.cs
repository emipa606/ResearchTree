// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class ResearchNode : Node
{
    private static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache = [];

    private static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache = [];

    private readonly int cacheOrder;

    public readonly ResearchProjectDef Research;

    private bool availableCache;

    private int currentCacheOrder;

    private bool hasRefreshedAvailability;
    private bool hasRefreshedBuildings;
    private bool hasRefreshedFacilities;


    public ResearchNode(ResearchProjectDef research, int order)
    {
        Research = research;
        _pos = new Vector2(0f, research.researchViewY + 1f);
        cacheOrder = order;
        currentCacheOrder = Assets.TotalAmountOfResearch;
    }

    public List<ResearchNode> Parents
    {
        get
        {
            var enumerable = InNodes.OfType<ResearchNode>();
            enumerable = enumerable.Concat(from dn in InNodes.OfType<DummyNode>()
                select dn.Parent);
            return enumerable.ToList();
        }
    }

    public override Color Color
    {
        get
        {
            if (Highlighted)
            {
                return GenUI.MouseoverColor;
            }

            if (Completed)
            {
                return Assets.ColorCompleted.TryGetValue(Research.techLevel);
            }

            return Available
                ? Assets.ColorCompleted.TryGetValue(Research.techLevel)
                : Assets.ColorUnavailable.TryGetValue(Research.techLevel);
        }
    }

    public override Color EdgeColor
    {
        get
        {
            if (Highlighted)
            {
                return GenUI.MouseoverColor;
            }

            if (Completed)
            {
                return Assets.ColorCompleted.TryGetValue(Research.techLevel);
            }

            return Available
                ? Assets.ColorAvailable.TryGetValue(Research.techLevel)
                : Assets.ColorUnavailable.TryGetValue(Research.techLevel);
        }
    }

    public List<ResearchNode> Children
    {
        get
        {
            var enumerable = OutNodes.OfType<ResearchNode>();
            enumerable = enumerable.Concat(from dn in OutNodes.OfType<DummyNode>()
                select dn.Child);
            return enumerable.ToList();
        }
    }

    public override string Label => Research.LabelCap;

    public override bool Completed => Research.IsFinished;

    public override bool Available
    {
        get
        {
            if (Research.IsFinished || Research.IsHidden)
            {
                return false;
            }

            return FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeDoNotGenerateResearchTree
                   || DebugSettings.godMode || getCacheValue();
        }
    }

    public void ClearInstanceCaches()
    {
        hasRefreshedAvailability = false;
        hasRefreshedBuildings = false;
        hasRefreshedFacilities = false;
        currentCacheOrder = cacheOrder;
    }

    private bool getCacheValue()
    {
        if (!Assets.RefreshResearch || hasRefreshedAvailability)
        {
            return availableCache;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch)
        {
            currentCacheOrder++;
            return availableCache;
        }

        hasRefreshedAvailability = true;

        if (MissingFacilities(Research).Any() ||
            (Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled) ||
            (Assets.UsingRimedieval && !Assets.AllowedResearchDefs.Contains(Research)) ||
            !Research.TechprintRequirementMet ||
            (Research.requiredResearchBuilding != null && !Research.PlayerHasAnyAppropriateResearchBench) ||
            !Research.PlayerMechanitorRequirementMet ||
            !Research.AnalyzedThingsRequirementsMet)
        {
            availableCache = false;
            return availableCache;
        }

        object[] parameters = [Research, null];
        if (Assets.UsingVanillaVehiclesExpanded)
        {
            var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, parameters);
            if (boolResult)
            {
                availableCache = false;
                return availableCache;
            }
        }

        if (Assets.IsBlockedByGrimworld(Research) ||
            Assets.IsBlockedByWorldTechLevel(Research) ||
            Assets.IsBlockedByMedievalOverhaul(Research))
        {
            availableCache = false;
            return availableCache;
        }

        if (Research.prerequisites?.Any() != true && Research.hiddenPrerequisites?.Any() != true)
        {
            availableCache = true;
            return availableCache;
        }

        if (Research.prerequisites?.Any() == true)
        {
            if (Research.prerequisites.Where(researchPrerequisite => !researchPrerequisite.IsFinished)
                .Any(researchPrerequisite => !researchPrerequisite.ResearchNode().Available))
            {
                availableCache = false;
                return availableCache;
            }
        }

        if (Research.hiddenPrerequisites?.Any() == true)
        {
            if (Research.hiddenPrerequisites.Where(researchPrerequisite => !researchPrerequisite.IsFinished)
                .Any(researchPrerequisite => !researchPrerequisite.ResearchNode().Available))
            {
                availableCache = false;
                return availableCache;
            }
        }

        availableCache = true;
        return availableCache;
    }

    public bool BuildingPresent(ResearchProjectDef research)
    {
        if (DebugSettings.godMode && Prefs.DevMode)
        {
            return true;
        }

        var hasCache = _buildingPresentCache.TryGetValue(research, out var value);

        if (!Assets.RefreshResearch && hasCache)
        {
            return value;
        }

        if (hasRefreshedBuildings && hasCache)
        {
            return value;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch && hasCache)
        {
            currentCacheOrder++;
            return value;
        }

        hasRefreshedBuildings = true;

        value = research.requiredResearchBuilding == null || Find.Maps
            .SelectMany(map => map.listerBuildings.allBuildingsColonist).OfType<Building_ResearchBench>()
            .Any(b => research.CanBeResearchedAt(b, true));
        if (value)
        {
            value = research.Ancestors().All(BuildingPresent);
        }

        _buildingPresentCache[research] = value;
        return value;
    }

    public static implicit operator ResearchNode(ResearchProjectDef def)
    {
        return def.ResearchNode();
    }

    public List<ThingDef> MissingFacilities(ResearchProjectDef research)
    {
        var hasCache = _missingFacilitiesCache.TryGetValue(research, out var value);

        if (!Assets.RefreshResearch && hasCache)
        {
            return value;
        }

        if (hasRefreshedFacilities && hasCache)
        {
            return value;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch && hasCache)
        {
            currentCacheOrder++;
            return value;
        }

        hasRefreshedFacilities = true;

        var list = research.Ancestors().Where(rpd => !rpd.IsFinished && !rpd.PlayerHasAnyAppropriateResearchBench)
            .ToList();
        list.Add(research);
        var availableBenches = Find.Maps.SelectMany(map => map.listerBuildings.allBuildingsColonist)
            .OfType<Building_ResearchBench>();
        var distinctBenches = availableBenches.Select(b => b.def).Distinct();
        value = [];
        foreach (var item in list)
        {
            if (item.requiredResearchBuilding != null)
            {
                if (!distinctBenches.Contains(item.requiredResearchBuilding))
                {
                    value.Add(item.requiredResearchBuilding);
                }
            }

            if (item.requiredResearchFacilities.NullOrEmpty())
            {
                continue;
            }

            foreach (var facility in item.requiredResearchFacilities)
            {
                if (!availableBenches.Any(b => b.HasFacility(facility)))
                {
                    value.Add(facility);
                }
            }
        }

        value = value.Distinct().ToList();
        _missingFacilitiesCache[research] = value;
        return value;
    }

    public bool BuildingPresent()
    {
        return BuildingPresent(Research);
    }

    public override bool IsVisible(Rect visibleRect)
    {
        return base.IsVisible(visibleRect) && !Assets.IsBlockedBySOS2(Research);
    }

    public override void Draw(Rect visibleRect, bool forceDetailedMode = false, bool drawLabel = true)
    {
        if (!IsVisible(visibleRect))
        {
            Highlighted = false;
            return;
        }

        if (MainTabWindow_ResearchTree.Instance.IsHighlighted(Research))
        {
            Highlighted = true;
        }

        var detailedMode = forceDetailedMode ||
                           MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff;
        var mouseOver = Mouse.IsOver(Rect) || Rect.Contains(Event.current.mousePosition);
        if (MainTabWindow_ResearchTree.Instance.IsQuickSearchWidgetActive())
        {
            mouseOver = false;
        }

        if (Event.current.type == EventType.Repaint)
        {
            // researches that are completed or could be started immediately, and that have the required building(s) available
            //GUI.color = Mouse.IsOver(Rect) ? GenUI.MouseoverColor : Color;
            var color = mouseOver ? GenUI.MouseoverColor : Color;

            if (mouseOver || Highlighted)
            {
                FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, color);
            }
            else
            {
                FastGUI.DrawTextureFast(Rect, Assets.Button, color);
            }

            // grey out center to create a progress bar effect, completely greying out research not started.
            if (Available)
            {
                var position = Rect.ContractedBy(3f);
                //GUI.color = Assets.ColorAvailable[Research.techLevel];
                color = Assets.ColorAvailable[Research.techLevel];
                position.xMin += Research.ApparentPercent() * position.width;
                FastGUI.DrawTextureFast(position, BaseContent.WhiteTex, color);
            }

            Highlighted = false;
            if (!Completed && !Available)
            {
                GUI.color = Color.grey;
            }
            else
            {
                GUI.color = Color.white;
            }

            if (detailedMode)
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = false;
                Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
                Widgets.Label(LabelRect, Research.LabelCap);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;
                Text.Font = GameFont.Medium;
                Widgets.Label(Rect, Research.LabelCap);
            }

            // draw research cost and icon
            if (detailedMode)
            {
                Text.Anchor = TextAnchor.UpperRight;
                var costString = $"{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                if (!Research.IsFinished)
                {
                    costString =
                        $"{Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer)}/{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                }

                Text.Font = costString.Length > 6 ? GameFont.Tiny : GameFont.Small;
                Widgets.Label(CostLabelRect, costString);
                var researchIcon = !Completed && !Available ? Assets.Lock : Assets.ResearchIcon;
                GUI.DrawTexture(CostIconRect, researchIcon, ScaleMode.ScaleToFit);
            }

            Text.WordWrap = true;
            BuildTips();

            // draw unlock icons
            if (detailedMode)
            {
                var unlockDefsAndDescs = Research.GetUnlockDefsAndDescs();
                for (var i = 0; i < unlockDefsAndDescs.Count; i++)
                {
                    var rect = new Rect(IconsRect.xMax - ((i + 1) * (Constants.IconSize.x + 4f)),
                        IconsRect.yMin + ((IconsRect.height - Constants.IconSize.y) / 2f), Constants.IconSize.x,
                        Constants.IconSize.y);
                    if (rect.xMin - Constants.IconSize.x < IconsRect.xMin && i + 1 < unlockDefsAndDescs.Count)
                    {
                        rect.x = IconsRect.x + 4f;
                        GUI.DrawTexture(rect, Assets.MoreIcon, ScaleMode.ScaleToFit);
                        var text = string.Join("\n",
                            (from p in unlockDefsAndDescs.GetRange(i, unlockDefsAndDescs.Count - i)
                                select p.Second).ToArray());
                        TooltipHandler.TipRegion(rect, text);
                        break;
                    }

                    Widgets.DefIcon(rect, unlockDefsAndDescs[i].First);
                    if (Queue._draggedNode == null)
                    {
                        TooltipHandler_Modified.TipRegion(rect, unlockDefsAndDescs[i].Second);
                    }
                }
            }

            if (mouseOver)
            {
                if (Completed)
                {
                    foreach (var child in Children)
                    {
                        child.Highlighted = true;
                    }
                }
                else
                {
                    Highlighted = true;
                    foreach (var item in GetMissingRequiredRecursive())
                    {
                        item.Highlighted = true;
                    }
                }
            }

        }

        if (drawLabel)
        {
            Queue.DrawOrderLabel(visibleRect, this);
        }

        if (!mouseOver || !Widgets.ButtonInvisible(Rect))
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick || Event.current.button == Constants.RightClick)
        {
            UI.UnfocusCurrentControl();
        }

        if (Event.current.button == Constants.RightClick && !Event.current.shift)
        {
            var researchCard = new Dialog_ResearchInfoCard(Research);
            Find.WindowStack.Add(researchCard);
            return;
        }

        if (!Available)
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick && Event.current.control && !Research.IsFinished)
        {
            Queue.EnqueueRangeFirst(GetMissingRequired());
        }

        if (Event.current.button == Constants.LeftClick && !Event.current.control && !Research.IsFinished)
        {
            if (!Queue.IsQueued(this))
            {
                Queue.EnqueueRange(GetMissingRequired(), Event.current.shift);
            }
            else
            {
                Queue.TryDequeue(this);
            }
        }

        if (Event.current.button != Constants.RightClick && !Event.current.shift)
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick && Event.current.shift)
        {
            return;
        }

        if (!DebugSettings.godMode || !Prefs.DevMode || Research.IsFinished)
        {
            return;
        }

        // Shift + RClick + dev.godMod  finish this and start next
        Queue.Notify_InstantFinished(this);
    }

    public List<ResearchNode> GetMissingRequired()
    {
        return GetMissingRequiredRecursive()
            .Concat(new List<ResearchNode>([this]))
            .Distinct()
            .ToList();
    }

    public List<ResearchNode> GetMissingRequiredRecursive()
    {
        var enumerable =
            (Research.prerequisites?.Where(rpd => !rpd.IsFinished) ?? Array.Empty<ResearchProjectDef>())
            .Concat(Research.hiddenPrerequisites?.Where(rpd => !rpd.IsFinished) ?? Array.Empty<ResearchProjectDef>())
            .Select(rpd => rpd.ResearchNode())
            .ToList();

        var list = new List<ResearchNode>(enumerable);
        foreach (var item in enumerable)
        {
            list.AddRange(item.GetMissingRequiredRecursive());
        }

        return list.Distinct().ToList();
    }

    public List<ThingDef> MissingFacilities()
    {
        return MissingFacilities(Research);
    }

    private void BuildTips()
    {
        if (Queue._draggedNode != null)
        {
            return;
        }

        var tooltipstring = GetResearchTooltipString();
        var missingFacilities = MissingFacilities();
        if (missingFacilities?.Any() == true)
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.MissingFacilities".Translate(string.Join(", ",
                missingFacilities.Select(td => td.LabelCap).ToArray())));
        }

        if (!Research.TechprintRequirementMet)
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.MissingTechprints".Translate(Research.TechprintsApplied,
                Research.techprintCount));
        }

        if (!Research.AnalyzedThingsRequirementsMet)
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.MissingStudiedThings".Translate(string.Join(", ",
                Research.requiredAnalyzed.Select(def => def.LabelCap))));
        }

        if (!Research.PlayerMechanitorRequirementMet)
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.MissingMechanitorRequirement".Translate());
        }

        if (Assets.UsingVanillaVehiclesExpanded)
        {
            var valueArray = new object[] { Research, null };
            var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, valueArray);

            if (boolResult)
            {
                tooltipstring.AppendLine();
                var wreck = (ThingDef)valueArray[1];
                if (wreck != null)
                {
                    tooltipstring.AppendLine();
                    tooltipstring.AppendLine("VVE_WreckNotRestored".Translate(wreck.LabelCap));
                }
            }
        }

        if (Assets.IsBlockedByGrimworld(Research))
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.GrimworldDoesNotAllow".Translate());
        }

        if (Assets.IsBlockedByWorldTechLevel(Research))
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.WorldTechLevelDoesNotAllow".Translate());
        }

        if (Assets.IsBlockedByMedievalOverhaul(Research))
        {
            tooltipstring.AppendLine();
            if (Assets.TryGetBlockingSchematicFromMedievalOverhaul(Research, out var thingLabel))
            {
                tooltipstring.AppendLine("DankPyon_RequiredSchematic".Translate() + $": {thingLabel}");
            }
            else
            {
                tooltipstring.AppendLine("DankPyon_RequiredSchematic".Translate());
            }
        }

        if (Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled)
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.SemiRandomResearchLoaded".Translate());
        }

        if (Assets.UsingRimedieval && !Assets.AllowedResearchDefs.Contains(Research))
        {
            tooltipstring.AppendLine();
            tooltipstring.AppendLine("Fluffy.ResearchTree.RimedievalDoesNotAllow".Translate());
        }

        TooltipHandler_Modified.TipRegion(Rect, tooltipstring.ToString());
    }

    private StringBuilder GetResearchTooltipString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(Research.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n");
        stringBuilder.AppendLine(Research.description);

        stringBuilder.AppendLine();
        // TODO: Add settings so that shortcut key tips can be hidden
        if (Queue.IsQueued(this))
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickRemoveFromQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickMoveToFrontOfQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }
        else
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickReplaceQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.SLClickAddToQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickAddToFrontOfQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }

        stringBuilder.AppendLine(
            "Fluffy.ResearchTree.SRClickShowInfo".Translate().Colorize(ColoredText.SubtleGrayColor));
        if (DebugSettings.godMode)
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.RClickInstaFinishNew".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }

        return stringBuilder;
    }

    public void DrawAt(Vector2 pos, Rect visibleRect, bool forceDetailedMode = false)
    {
        SetRects(pos);
        Draw(visibleRect, forceDetailedMode, false);
        SetRects();
    }

    // TODO: The above code for handling key events should be extracted into a public method,
    //       so that the consistency of shortcut keys can be maintained.
    //       However, left-clicking on a node will probably conflict with the original view.
    //       We will try it out, but that’s it for now.
    public void HandleVanillaNodeClickEvent()
    {
        // Don't understand what this sign does, but it needed.
        Assets.RefreshResearch = true;
        if (!Available)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) && !Completed)
        {
            if (!Queue.IsQueued(this))
            {
                Queue.EnqueueRange(GetMissingRequired(), Event.current.shift);
            }
            else
            {
                Queue.TryDequeue(this);
            }
        }
        else if (Input.GetKey(KeyCode.LeftControl) && !Completed)
        {
            Queue.EnqueueRangeFirst(GetMissingRequired());
        }
    }
}