using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), "ComputeUnlockedDefsThatHaveMissingMemes")]
public static class MainTabWindow_Research_ComputeUnlockedDefsThatHaveMissingMemes
{
    public static bool Prefix(ref List<(BuildableDef, List<string>)> __result)
    {
        __result = [];
        return !FluffyResearchTreeMod.instance.Settings.NoIdeologyPopup;
    }
}