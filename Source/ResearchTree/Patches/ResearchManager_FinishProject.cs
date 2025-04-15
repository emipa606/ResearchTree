// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
public class ResearchManager_FinishProject
{
    private static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog)
    {
        if (doCompletionDialog && !proj.IsAnomalyResearch())
        {
            doCompletionDialog = FluffyResearchTreeMod.instance.Settings.ShowCompletion;
        }
    }

    private static void Postfix(ResearchProjectDef proj)
    {
        if (proj == null)
        {
            return;
        }

        Queue.TryStartNext(proj);
    }
}