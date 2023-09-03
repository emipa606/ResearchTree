// ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Reflection;
using HarmonyLib;
using Verse;

namespace FluffyResearchTree;

public class ResearchTree : Mod
{
    public ResearchTree(ModContentPack content)
        : base(content)
    {
        new Harmony("Fluffy.ResearchTree").PatchAll(Assembly.GetExecutingAssembly());
    }
}