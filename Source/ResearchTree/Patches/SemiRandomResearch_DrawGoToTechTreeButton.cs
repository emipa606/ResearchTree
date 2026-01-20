using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyResearchTree;

public static class SemiRandomResearch_DrawGoToTechTreeButton
{
    public static void ProgressionPostfix(Rect leftRect)
    {
        var relocatedRect = leftRect;
        relocatedRect.width /= 7f;
        relocatedRect.height = 48f;
        relocatedRect.y = leftRect.yMax - relocatedRect.height;
        Prefix(relocatedRect);
    }

    public static bool Prefix(Rect mainRect)
    {
        const float num = 32f;

        var rect = new Rect(mainRect.xMax - num - 6f, mainRect.yMin, num, num);
        TooltipHandler.TipRegion(rect,
            FluffyResearchTreeMod.instance.Settings.OverrideResearch
                ? "Fluffy.ResearchTree.HoldForClassic".Translate()
                : "Fluffy.ResearchTree.HoldForNew".Translate());
        if (!Widgets.ButtonTextSubtle(rect, "") && !Widgets.ButtonImage(rect, Assets.SemiRandomTexture2D))
        {
            return false;
        }

        SoundDefOf.ResearchStart.PlayOneShotOnCamera();
        var mainTabWindow = Find.WindowStack.WindowOfType<MainTabWindow>();
        var tabWindow = MainButtonDefOf.Research.TabWindow;
        if (mainTabWindow == null || tabWindow == null)
        {
            return false;
        }

        Find.WindowStack.TryRemove(mainTabWindow, false);
        var researchTab = MainButtonDefOf.Research;
        ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Main;

        if (FluffyResearchTreeMod.instance.Settings.LoadType != Constants.LoadTypeDoNotGenerateResearchTree &&
            Tree.Initialized)
        {
            switch (FluffyResearchTreeMod.instance.Settings.OverrideResearch)
            {
                // LeftCtrl is the way of Dubs Mint Menus mod
                case true when
                    !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl):
                case false when
                    Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl):
                    researchTab = Assets.MainButtonDefOf.FluffyResearchTree;
                    break;
            }
        }

        Find.WindowStack.Add(researchTab.TabWindow);
        SoundDefOf.TabOpen.PlayOneShotOnCamera();
        return false;
    }
}