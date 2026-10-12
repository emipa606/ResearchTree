using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), "AttemptBeginResearch")]
public static class MainTabWindow_Research_AttemptBeginResearch
{
    private static readonly MethodInfo Dialog_MessageBox_CreateConfirmationMethodInfo =
        AccessTools.Method(typeof(Dialog_MessageBox), nameof(Dialog_MessageBox.CreateConfirmation),
            [typeof(TaggedString), typeof(Action), typeof(bool), typeof(string), typeof(WindowLayer)]);

    private static readonly MethodInfo Queue_CreateConfirmationMethodInfo =
        AccessTools.Method(typeof(Queue), nameof(Queue.CreateConfirmation),
        [
            typeof(ResearchProjectDef), typeof(TaggedString), typeof(Action), typeof(bool), typeof(string),
            typeof(WindowLayer)
        ]);

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
    {
        var instructions = instr.ToList();

        var startIndex = -1;
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(Dialog_MessageBox_CreateConfirmationMethodInfo))
            {
                startIndex = i;
            }
        }

        if (startIndex <= -1)
        {
            return codes.AsEnumerable();
        }

        var opts = new List<CodeInstruction>
        {
            // Dialog_MessageBox.CreateConfirmation(...
            new(codes[9]), // ldloc.0
            new(codes[10]) // ldfld   projectToStart
        };

        codes.InsertRange(startIndex - 9, opts);
        codes[startIndex + 2].operand = Queue_CreateConfirmationMethodInfo;

        return codes.AsEnumerable();
    }
}