using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
public static class ResearchManager_StopProject
{
    public static void Postfix(ResearchProjectDef proj)
    {
        if (proj == null || proj.IsAnomalyResearch())
        {
            return;
        }

        Queue.TryStartNext(proj);
    }
}