// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.DoBeginResearch))]
public class MainTabWindow_Research_DoBeginResearch
{
    private static void Prefix(ResearchProjectDef projectToStart)
    {
        Queue.DoBeginResearch(projectToStart);
    }
}