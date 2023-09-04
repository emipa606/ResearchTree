using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class Dialog_ResearchInfoCard : Window
{
    private static readonly Color alternateBackground = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    private readonly ResearchProjectDef researchProjectDef;
    private Vector2 scrollPosition = Vector2.zero;

    public Dialog_ResearchInfoCard(ResearchProjectDef researchProject)
    {
        researchProjectDef = researchProject;
        Setup();
    }

    public override Vector2 InitialSize => new Vector2(950f, 760f);

    public override void DoWindowContents(Rect inRect)
    {
        var mainRect = new Rect(inRect);
        mainRect = mainRect.ContractedBy(18f);
        mainRect.height -= 20f;

        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(mainRect);
        Text.Font = GameFont.Medium;
        listing_Standard.Label(GetTitle());
        Text.Font = GameFont.Small;

        if (!string.IsNullOrEmpty(researchProjectDef.modContentPack?.Name))
        {
            listing_Standard.Label(researchProjectDef.modContentPack.Name);
        }

        listing_Standard.Label(researchProjectDef.description);
        listing_Standard.GapLine();
        listing_Standard.End();

        var unlockDefsAndDescs = researchProjectDef.GetUnlockDefsAndDescs();
        if (!unlockDefsAndDescs.Any())
        {
            return;
        }

        var borderRect = mainRect;
        borderRect.y += listing_Standard.CurHeight + 12f;
        borderRect.height -= listing_Standard.CurHeight + 12f;
        var scrollContentRect = borderRect;
        scrollContentRect.height = unlockDefsAndDescs.Count * (Constants.LargeIconSize.y + 2f);
        scrollContentRect.width -= 20;
        scrollContentRect.x = 0;
        scrollContentRect.y = 0;


        var scrollListing = new Listing_Standard();
        Widgets.BeginScrollView(borderRect, ref scrollPosition, scrollContentRect);
        scrollListing.Begin(scrollContentRect);
        var alternate = true;
        foreach (var unlockedThing in unlockDefsAndDescs)
        {
            scrollListing.Gap(1f);
            var rect = scrollListing.GetRect(Constants.LargeIconSize.y);
            alternate = !alternate;
            if (alternate)
            {
                var alternateRect = rect;
                alternateRect.width = scrollContentRect.width;
                Widgets.DrawBoxSolid(rect.ExpandedBy(1), alternateBackground);
            }

            rect.width = Constants.LargeIconSize.x;
            unlockedThing.First.DrawColouredIcon(rect);
            var textRect = rect;
            textRect.x += Constants.LargeIconSize.x + 2f;
            textRect.width = scrollContentRect.width - Constants.LargeIconSize.x - 2f;
            Widgets.Label(textRect, unlockedThing.Second);
            scrollListing.Gap(1f);
        }

        scrollListing.End();
        Widgets.EndScrollView();
    }

    private string GetTitle()
    {
        if (researchProjectDef == null)
        {
            return string.Empty;
        }

        return researchProjectDef.LabelCap;
    }

    private void Setup()
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