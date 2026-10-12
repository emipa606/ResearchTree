using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(WindowStack), "AdjustWindowsIfResolutionChanged")]
public static class WindowStack_AdjustWindowsIfResolutionChanged
{
    public static void Prefix(IntVec2 ___prevResolution, out IntVec2 __state)
    {
        __state = ___prevResolution;
    }

    public static void Postfix(IntVec2 ___prevResolution, List<Window> ___windows, IntVec2 __state)
    {
        if (___prevResolution == __state)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        if (FluffyResearchTreeMod.instance?.Settings.BlockRegenerationOnResolutionChange == true)
        {
            return;
        }

        var treeWindow = ___windows?.FirstOrDefault(window => window is MainTabWindow_ResearchTree);
        if (treeWindow is { IsOpen: true })
        {
            treeWindow.Close();
        }

        Tree.Reset(false);
    }
}