using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainButtonWorker), nameof(MainButtonWorker.DoButton))]
public class MainButtonWorker_DoButton
{
    private static void Postfix(Rect rect, MainButtonDef ___def)
    {
        if (___def != MainButtonDefOf.Research || Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled)
        {
            return;
        }

        Queue.DrawLabelForMainButton(rect);

        if (FluffyResearchTreeMod.instance.Settings.DoNotGenerateResearchTree())
        {
            return;
        }

        if (!Tree.Instance.Initialized)
        {
            TooltipHandler.TipRegion(rect, "Fluffy.ResearchTree.LoadingWait".Translate());
            return;
        }

        TooltipHandler.TipRegion(rect,
            FluffyResearchTreeMod.instance.Settings.OverrideResearch
                ? "Fluffy.ResearchTree.HoldForClassic".Translate()
                : "Fluffy.ResearchTree.HoldForNew".Translate());
    }
}