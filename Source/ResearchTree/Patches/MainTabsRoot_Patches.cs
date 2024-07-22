﻿using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabsRoot))]
public class MainTabsRoot_Patches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainTabsRoot.ToggleTab))]
    public static void ToggleTabPrefix(ref MainButtonDef newTab)
    {
        if (newTab == null || newTab != MainButtonDefOf.Research)
        {
            return;
        }

        if (ModsConfig.AnomalyActive && ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab == ResearchTabDefOf.Anomaly)
        {
            return;
        }

        switch (FluffyResearchTreeMod.instance.Settings.OverrideResearch)
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
}