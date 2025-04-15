using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyResearchTree;

public static class SemiRandomResearchProgressionFork_ButtonText
{
    public static bool Prefix(Rect rect, string label)
    {
        if (label != "Research Tree")
        {
            return true;
        }

        TooltipHandler.TipRegion(rect,
            FluffyResearchTreeMod.instance.Settings.OverrideResearch
                ? "Fluffy.ResearchTree.HoldForClassic".Translate()
                : "Fluffy.ResearchTree.HoldForNew".Translate());
        if (!Widgets.ButtonTextSubtle(rect, label))
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

        if (Tree.Instance.Initialized)
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