using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.AdjustWindowsIfResolutionChanged))]
public class WindowStack_AdjustWindowsIfResolutionChanged
{
    private static void Prefix(IntVec2 ___prevResolution, out IntVec2 __state)
    {
        __state = ___prevResolution;
    }

    private static void Postfix(IntVec2 ___prevResolution, List<Window> ___windows, IntVec2 __state)
    {
        if (___prevResolution == __state)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        var treeWindow = ___windows?.FirstOrDefault(window => window is MainTabWindow_ResearchTree);
        if (treeWindow is { IsOpen: true })
        {
            treeWindow.Close();
        }

        Tree.Instance.Reset();
    }
}