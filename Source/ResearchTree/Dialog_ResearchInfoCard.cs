using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class Dialog_ResearchInfoCard : Window
{
    private static readonly Color alternateBackground = new(0.2f, 0.2f, 0.2f, 0.5f);
    private readonly ResearchProjectDef researchProjectDef;
    private Vector2 scrollPosition = Vector2.zero;

    public Dialog_ResearchInfoCard(ResearchProjectDef researchProject)
    {
        researchProjectDef = researchProject;
        setup();
    }

    public override Vector2 InitialSize => new(955f, 765f);

    public override void PreOpen()
    {
        TooltipHandler_Modified.GloballyDisabled = true;
        base.PreOpen();
    }

    public override void PostClose()
    {
        base.PostClose();
        TooltipHandler_Modified.GloballyDisabled = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var mainRect = new Rect(inRect);
        mainRect = mainRect.ContractedBy(18f);
        mainRect.height -= 20f;

        var listingStandard = new Listing_Standard();
        listingStandard.Begin(mainRect);
        Text.Font = GameFont.Medium;
        listingStandard.Label(getTitle());
        Text.Font = GameFont.Small;

        if (!string.IsNullOrEmpty(researchProjectDef.modContentPack?.Name))
        {
            listingStandard.Label(researchProjectDef.modContentPack.Name);
        }

        if (researchProjectDef.IsFinished)
        {
            listingStandard.Label("Finished".Translate());
        }
        else
        {
            if (researchProjectDef.ProgressReal > 0)
            {
                listingStandard.Label("Fluffy.ResearchTree.InProgress".Translate(researchProjectDef.ProgressReal,
                    researchProjectDef.baseCost));
            }
            else
            {
                listingStandard.Label("Fluffy.ResearchTree.NotStarted".Translate(researchProjectDef.baseCost));
            }
        }

        listingStandard.Label(researchProjectDef.description);

        if (Assets.IsBlockedByGrimworld(researchProjectDef))
        {
            Assets.GrimworldInfoMethod.Invoke(null, [null, listingStandard.GetRect(110f), 1f, researchProjectDef]);
        }

        if (Assets.IsBlockedByWorldTechLevel(researchProjectDef))
        {
            listingStandard.Label("Fluffy.ResearchTree.WorldTechLevelDoesNotAllow".Translate());
        }

        if (Assets.IsBlockedByMedievalOverhaul(researchProjectDef))
        {
            if (Assets.TryGetBlockingSchematicFromMedievalOverhaul(researchProjectDef, out var thingLabel))
            {
                listingStandard.Label("DankPyon_RequiredSchematic".Translate() + $": {thingLabel}");
            }
            else
            {
                listingStandard.Label("DankPyon_RequiredSchematic".Translate());
            }
        }

        listingStandard.GapLine();
        listingStandard.End();

        var unlockDefsAndDescs = researchProjectDef.GetUnlockDefsAndDescriptions();
        if (!unlockDefsAndDescs.Any())
        {
            return;
        }

        var borderRect = mainRect;
        borderRect.y += listingStandard.CurHeight + 12f;
        borderRect.height -= listingStandard.CurHeight + 12f;
        var scrollContentRect = borderRect;
        scrollContentRect.height = unlockDefsAndDescs.Count * (Constants.LargeIconSize.y + 2f);
        scrollContentRect.width -= 20;
        scrollContentRect.x = 0;
        scrollContentRect.y = 0;


        var scrollListing = new Listing_Standard();
        var researchTreeInstance = MainTabWindow_ResearchTree.Instance;
        Widgets.BeginScrollView(borderRect, ref scrollPosition, scrollContentRect);
        scrollListing.Begin(scrollContentRect);

        try
        {
            var alternate = true;
            foreach (var unlockedThing in unlockDefsAndDescs.OrderBy(pair => pair.Second))
            {
                scrollListing.Gap(1f);
                var rect = scrollListing.GetRect(Constants.LargeIconSize.y);
                alternate = !alternate;
                var alternateRect = rect;
                alternateRect.width = scrollContentRect.width;
                if (alternate)
                {
                    Widgets.DrawBoxSolid(rect.ExpandedBy(0, 1), alternateBackground);
                }

                if (Mouse.IsOver(alternateRect))
                {
                    Widgets.DrawBox(rect.ExpandedBy(0, 1));
                }

                if (researchTreeInstance != null && researchTreeInstance.IsQuickSearchWidgetActive() &&
                    researchTreeInstance.MatchesUnlockedDef(unlockedThing.First))
                {
                    Widgets.DrawTextHighlight(rect);
                }

                if (Widgets.ButtonInvisible(alternateRect))
                {
                    var itemInfocard = new Dialog_InfoCard(unlockedThing.First);
                    Find.WindowStack.Add(itemInfocard);
                }

                rect.width = Constants.LargeIconSize.x;
                Widgets.DefIcon(rect, unlockedThing.First);
                //unlockedThing.First.DrawColouredIcon(rect);
                var textRect = rect;
                textRect.x += Constants.LargeIconSize.x + 2f;
                textRect.width = scrollContentRect.width - Constants.LargeIconSize.x - 2f;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(textRect, unlockedThing.Second);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                scrollListing.Gap(1f);
            }
        }
        finally
        {
            scrollListing.End();
            Widgets.EndScrollView();
        }
    }

    private string getTitle()
    {
        if (researchProjectDef == null)
        {
            return string.Empty;
        }

        return researchProjectDef.LabelCap;
    }

    private void setup()
    {
        forcePause = true;
        doCloseButton = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        soundAppear = SoundDefOf.InfoCard_Open;
        soundClose = SoundDefOf.InfoCard_Close;
    }
}