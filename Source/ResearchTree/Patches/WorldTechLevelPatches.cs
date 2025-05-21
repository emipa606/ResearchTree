using System.Reflection;
using HarmonyLib;
using Verse;

namespace FluffyResearchTree;

[StaticConstructorOnStartup]
public static class WorldTechLevelPatches
{
    static WorldTechLevelPatches()
    {
        if (!Prepare())
        {
            return;
        }

        // Note: The patch must be applied from a static constructor.
        // Using the attribute-based approach [HarmonyPatch] may result in TargetMethod not being found.
        FluffyResearchTreeMod.Harmony.Patch(TargetMethod(),
                                            postfix: new HarmonyMethod(typeof(WorldTechLevelPatches), nameof(Postfix)));
    }

    private static bool Prepare()
    {
        return ModsConfig.IsActive("m00nl1ght.WorldTechLevel");
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.PropertySetter("WorldTechLevel.GameComponent_TechLevel:WorldTechLevel");
    }

    public static void Postfix()
    {
        if (!Tree.Initialized)
        {
            return;
        }

        Tree.Reset(false);
        Queue.Instance?.Clear();
    }
}