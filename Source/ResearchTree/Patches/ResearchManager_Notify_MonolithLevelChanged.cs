using HarmonyLib;
using RimWorld;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.Notify_MonolithLevelChanged))]
public class ResearchManager_Notify_MonolithLevelChanged
{
    private static void Postfix(int newLevel)
    {
        if (newLevel <= 0)
        {
            return;
        }

        Tree.Instance.Reset();
    }
}