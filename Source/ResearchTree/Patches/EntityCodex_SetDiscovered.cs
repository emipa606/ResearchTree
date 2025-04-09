using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(EntityCodex), nameof(EntityCodex.SetDiscovered), typeof(EntityCodexEntryDef), typeof(ThingDef), typeof(Thing))]
public class EntityCodex_SetDiscovered
{
    private static readonly MethodInfo Find_LetterStack = AccessTools.PropertyGetter(typeof(Find), nameof(Find.LetterStack));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
    {
        var instructions = instr.ToList();

        var startIndex = -1;
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(Find_LetterStack))
            {
                continue;
            }
            startIndex = i;
            break;
        }

        if (startIndex <= -1)
        {
            return codes.AsEnumerable();
        }

        var opts = new List<CodeInstruction>
        {
            new(OpCodes.Call, AccessTools.Method(typeof(Tree), nameof(Tree.EntityDiscovered))) // call  void FluffyResearchTree.Tree::EntityDiscovered()
        };

        codes.InsertRange(startIndex, opts);

        return codes.AsEnumerable();
    }
}