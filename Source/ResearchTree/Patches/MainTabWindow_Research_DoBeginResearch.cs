// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), "DoBeginResearch")]
public static class MainTabWindow_Research_DoBeginResearch
{
    public static void Prefix(ResearchProjectDef projectToStart)
    {
        if (projectToStart.IsAnomalyResearch())
        {
            return;
        }

        var researchNode = projectToStart.ResearchNode();
        var researchNodes = researchNode.GetMissingRequired();
        // check is same order
        if (Queue.IsEnqueueRangeFirstSameOrder(researchNodes, false, false))
        {
            return;
        }

        Queue.EnqueueFirst(researchNodes);
    }
}