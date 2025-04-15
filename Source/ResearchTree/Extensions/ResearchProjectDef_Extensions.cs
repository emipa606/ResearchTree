// ResearchProjectDef_Extensions.cs
// Copyright Karel Kroeze, 2019-2020

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class ResearchProjectDef_Extensions
{
    private static readonly Dictionary<Def, List<Pair<Def, string>>> _unlocksCache =
        new Dictionary<Def, List<Pair<Def, string>>>();

    private static Dictionary<Def, List<ResearchProjectDef>> _unlockedByCache =
        new Dictionary<Def, List<ResearchProjectDef>>();

    public static Dictionary<Def, List<ResearchProjectDef>> unlockedByCache
    {
        get
        {
            if (_unlockedByCache.Any())
            {
                return _unlockedByCache;
            }

            var dictionary = new Dictionary<Def, List<ResearchProjectDef>>();
            foreach (var allDef in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                foreach (var unlockedDef in allDef.UnlockedDefs)
                {
                    if (!dictionary.TryGetValue(unlockedDef, out var value))
                    {
                        value = [];
                        dictionary.Add(unlockedDef, value);
                    }

                    value.Add(allDef);
                }
            }

            _unlockedByCache = dictionary;
            return _unlockedByCache;
        }
    }


    public static List<ResearchProjectDef> Descendants(this ResearchProjectDef research)
    {
        var hashSet = new HashSet<ResearchProjectDef>();
        var queue = new Queue<ResearchProjectDef>(
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(res =>
                res.prerequisites?.Contains(research) ?? false));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            hashSet.Add(current);
            foreach (var item in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(res =>
                         res.prerequisites?.Contains(current) ?? false))
            {
                queue.Enqueue(item);
            }
        }

        return hashSet.ToList();
    }

    public static IEnumerable<ThingDef> GetPlantsUnlocked(this ResearchProjectDef research)
    {
        return DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
            (td.plant?.sowResearchPrerequisites?.Contains(research)).GetValueOrDefault()).OrderBy(def => def.label);
    }

    public static List<ResearchProjectDef> Ancestors(this ResearchProjectDef research)
    {
        var list = new List<ResearchProjectDef>();
        if (research.prerequisites.NullOrEmpty())
        {
            return list;
        }

        var stack = new Stack<ResearchProjectDef>(research.prerequisites.Where(parent => parent != research));
        while (stack.Count > 0)
        {
            var researchProjectDef = stack.Pop();
            list.Add(researchProjectDef);
            if (researchProjectDef.prerequisites.NullOrEmpty())
            {
                continue;
            }

            foreach (var prerequisite in researchProjectDef.prerequisites)
            {
                if (prerequisite != researchProjectDef && !list.Contains(prerequisite))
                {
                    stack.Push(prerequisite);
                }
            }
        }

        return list.Distinct().ToList();
    }

    public static IEnumerable<RecipeDef> GetRecipesUnlocked(this ResearchProjectDef research)
    {
        var first = DefDatabase<RecipeDef>.AllDefsListForReading.Where(rd => rd.researchPrerequisite == research);
        var second = from rd in DefDatabase<ThingDef>.AllDefsListForReading.Where(delegate(ThingDef td)
            {
                var researchPrerequisites = td.researchPrerequisites;
                return researchPrerequisites != null && researchPrerequisites.Contains(research) &&
                       !td.AllRecipes.NullOrEmpty();
            }).SelectMany(td => td.AllRecipes)
            where rd.researchPrerequisite == null
            select rd;
        return first.Concat(second).Distinct().OrderBy(def => def.label);
    }

    public static IEnumerable<TerrainDef> GetTerrainUnlocked(this ResearchProjectDef research)
    {
        return DefDatabase<TerrainDef>.AllDefsListForReading.Where(td =>
                unlockedByCache.TryGetValue(td, out var researchList) && researchList.Contains(research))
            .OrderBy(def => def.label);
    }

    public static IEnumerable<ThingDef> GetThingsUnlocked(this ResearchProjectDef research)
    {
        return DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
                unlockedByCache.TryGetValue(td, out var researchList) && researchList.Contains(research))
            .OrderBy(def => def.label);
    }

    public static List<Pair<Def, string>> GetUnlockDefsAndDescs(this ResearchProjectDef research, bool dedupe = true)
    {
        if (_unlocksCache.TryGetValue(research, out var descs))
        {
            return descs;
        }

        var list = new List<Pair<Def, string>>();
        list.AddRange(from d in research.GetThingsUnlocked()
            where d.IconTexture() != null
            select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsBuildingX".Translate(d.LabelCap)));
        list.AddRange(from d in research.GetTerrainUnlocked()
            where d.IconTexture() != null
            select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsBuildingX".Translate(d.LabelCap)));
        list.AddRange(from d in research.GetRecipesUnlocked()
            where d.IconTexture() != null
            select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsCraftingX".Translate(d.LabelCap)));
        list.AddRange(from d in research.GetPlantsUnlocked()
            where d.IconTexture() != null
            select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsPlantingX".Translate(d.LabelCap)));
        var list2 = research.Descendants();
        if (dedupe && list2.Any())
        {
            var descendantUnlocks = research.Descendants().SelectMany(c => from u in c.GetUnlockDefsAndDescs(false)
                    select u.First).Distinct()
                .ToList();
            list = list.Where(u => !descendantUnlocks.Contains(u.First)).ToList();
        }

        _unlocksCache.Add(research, list);
        return list;
    }

    public static float ApparentPercent(this ResearchProjectDef research)
    {
        return Mathf.Clamp01(research.ProgressApparent / research.CostApparent);
    }

    public static ResearchNode ResearchNode(this ResearchProjectDef research)
    {
        var researchNode = Tree.ResearchToNode(research) as ResearchNode;
        if (researchNode == null)
        {
            // It would be better to use warning instead of error. This is just a reminder.
            // eg: RimFridge_PowerFactorSetting def is hidden, but it is also finished.
            // So the patch of "ResearchManager.FinishProject" method will be executed to jump here.
            Logging.Warning($"Node for {research.LabelCap} not found. Was it intentionally hidden or locked?", true);
        }

        return researchNode;
    }

    public static bool IsAnomalyResearch(this ResearchProjectDef research)
    {
        return ModsConfig.AnomalyActive && research.knowledgeCategory != null;
    }
}