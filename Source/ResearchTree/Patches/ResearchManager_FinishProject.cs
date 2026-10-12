// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
public static class ResearchManager_FinishProject
{
    public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog)
    {
        if (doCompletionDialog && !proj.IsAnomalyResearch())
        {
            doCompletionDialog = FluffyResearchTreeMod.instance.Settings.ShowCompletion;
        }
    }

    public static void Postfix(ResearchProjectDef proj)
    {
        if (proj == null || proj.IsAnomalyResearch())
        {
            return;
        }

        Queue.TryStartNext(proj);
    }
}