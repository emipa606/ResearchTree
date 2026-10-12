using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabsRoot), nameof(MainTabsRoot.ToggleTab))]
public static class MainTabsRoot_ToggleTab
{
    public static void Prefix(ref MainButtonDef newTab)
    {
        if (newTab == null || newTab != MainButtonDefOf.Research ||
            Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled)
        {
            return;
        }

        if (ModsConfig.AnomalyActive && ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab ==
            ResearchTabDefOf.Anomaly)
        {
            return;
        }

        if (Assets.BetterResearchTabLoaded)
        {
            newTab = Assets.BetterResearchTab;
        }

        if (Assets.OrganizedResearchTabLoaded)
        {
            newTab = Assets.OrganizedResearchTab;
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeDoNotGenerateResearchTree)
        {
            return;
        }

        switch (FluffyResearchTreeMod.instance.Settings.OverrideResearch &&
                (Tree.Initialized || FluffyResearchTreeMod.instance.Settings.LoadType !=
                    Constants.LoadTypeLoadInBackground))
        {
            // LeftCtrl is the way of Dubs Mint Menus mod
            case true when
                !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl):
            case false when
                Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl):
                newTab = Assets.MainButtonDefOf.FluffyResearchTree;
                break;
        }
    }

    public static void Postfix()
    {
        if (Find.MainTabsRoot.OpenTab == null)
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = null;
        }
    }
}