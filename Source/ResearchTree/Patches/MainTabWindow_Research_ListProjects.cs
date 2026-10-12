using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), "ListProjects")]
public static class MainTabWindow_Research_ListProjects
{
    private static readonly MethodInfo CustomButtonTextMethodInfo =
        AccessTools.Method(typeof(Widgets), nameof(Widgets.CustomButtonText));

    /// <summary>
    ///     Determine the index by viewing the IL code of the MainTabWindow_Research.ListProjects method through dnSpy.
    /// </summary>
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr,
        ILGenerator generator)
    {
        var instructions = instr.ToList();

        var startIndex = -1;
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count - 18; i++)
        {
            if (
                codes[i].opcode == OpCodes.Ldc_I4_1 &&
                codes[i + 1].opcode == OpCodes.Br_S &&
                codes[i + 2].opcode == OpCodes.Ldc_I4_2 &&
                codes[i + 3].opcode == OpCodes.Stloc_S &&
                codes[i + 4].opcode == OpCodes.Ldloca_S &&
                codes[i + 18].Calls(CustomButtonTextMethodInfo)
            )
            {
                startIndex = i + 4;
            }
        }

        if (startIndex <= -1)
        {
            return codes.AsEnumerable();
        }

        CodeInstruction codeInstruction0 = null;
        CodeInstruction codeInstruction1 = null;
        CodeInstruction codeInstruction2 = null;
        // ... && !project.IsHidden
        if (codes[startIndex - 4 + 18 + 3].opcode == OpCodes.Ldfld &&
            codes[startIndex - 4 + 18 + 2].opcode == OpCodes.Ldloc_S)
        {
            codeInstruction0 = new CodeInstruction(codes[startIndex]);
            codeInstruction1 = new CodeInstruction(codes[startIndex - 4 + 18 + 2]);
            codeInstruction2 = new CodeInstruction(codes[startIndex - 4 + 18 + 3]);
        }

        if (codeInstruction1 == null || codes.Count < 30)
        {
            Logging.Error("Can not find code instruction position. Maybe RimWorld tweaked the source code.",
                true);
            return codes.AsEnumerable();
        }

        var opts = new List<CodeInstruction>();
        codeInstruction0?.opcode = OpCodes.Ldloc_S;
        opts.Add(codeInstruction0); // ldloc.s 
        opts.Add(codeInstruction1); // ldloc.s
        opts.Add(codeInstruction2); // ldfld
        opts.Add(new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(Queue), nameof(Queue.DrawLabelForVanillaWindow))));
        opts.Add(new CodeInstruction(OpCodes.Nop)); // nop

        // Color color7 = GUI.color; -> later
        var insertIndex = startIndex - 4 + 18 + 15;
        codes.InsertRange(insertIndex, opts);

        // SoundDefOf.Click.PlayOneShotOnCamera(); -> before
        insertIndex = startIndex - 4 + 18 + 3 + 3;

        var soundDefOf_Click_PlayOneShotOnCamera_Label = generator.DefineLabel();
        codes[insertIndex].labels.Add(soundDefOf_Click_PlayOneShotOnCamera_Label);

        var handleVanillaNodeClickEvent_Label = generator.DefineLabel();
        var handleVanillaNodeClickEvent = new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(ResearchNode), nameof(ResearchNode.HandleVanillaNodeClickEvent)));
        handleVanillaNodeClickEvent.labels.Add(handleVanillaNodeClickEvent_Label);

        opts.Clear();
        opts.Add(codeInstruction1); // ldloc.s
        opts.Add(codeInstruction2); // ldfld
        opts.Add(new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(ResearchProjectDef_Extensions),
                nameof(ResearchProjectDef_Extensions.ResearchNode))));
        opts.Add(new CodeInstruction(OpCodes.Dup)); // dup
        opts.Add(new CodeInstruction(OpCodes.Brtrue_S, handleVanillaNodeClickEvent_Label)); // brtrue.s label
        opts.Add(new CodeInstruction(OpCodes.Pop)); // pop
        opts.Add(new CodeInstruction(OpCodes.Br_S, soundDefOf_Click_PlayOneShotOnCamera_Label));
        opts.Add(handleVanillaNodeClickEvent);
        opts.Add(new CodeInstruction(OpCodes.Nop)); // nop

        codes.InsertRange(insertIndex, opts);

        return codes.AsEnumerable();
    }
}