// ResearchProjectDef_Extensions.cs
// Copyright Karel Kroeze, 2019-2020

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class ResearchProjectDef_Extensions
{
    private static readonly Dictionary<Def, List<Pair<Def, string>>> _unlocksCache = new();

    private static Dictionary<Def, List<ResearchProjectDef>> unlockedByCache
    {
        get
        {
            if (field.Any())
            {
                return field;
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

            field = dictionary;
            return field;
        }
    } = new();


    extension(ResearchProjectDef research)
    {
        public List<ResearchProjectDef> Descendants()
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

        private IEnumerable<ThingDef> getPlantsUnlocked()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
                (td.plant?.sowResearchPrerequisites?.Contains(research)).GetValueOrDefault()).OrderBy(def => def.label);
        }

        public List<ResearchProjectDef> Ancestors()
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

        private IEnumerable<RecipeDef> getRecipesUnlocked()
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

        private IEnumerable<TerrainDef> getTerrainUnlocked()
        {
            return DefDatabase<TerrainDef>.AllDefsListForReading.Where(td =>
                    unlockedByCache.TryGetValue(td, out var researchList) && researchList.Contains(research))
                .OrderBy(def => def.label);
        }

        private IEnumerable<ThingDef> getThingsUnlocked()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
                    unlockedByCache.TryGetValue(td, out var researchList) && researchList.Contains(research))
                .OrderBy(def => def.label);
        }

        public List<Pair<Def, string>> GetUnlockDefsAndDescriptions(bool dedupe = true)
        {
            if (_unlocksCache.TryGetValue(research, out var descs))
            {
                return descs;
            }

            var list = new List<Pair<Def, string>>();
            list.AddRange(from d in research.getThingsUnlocked()
                where d.IconTexture() != null
                select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsBuildingX".Translate(d.LabelCap)));
            list.AddRange(from d in research.getTerrainUnlocked()
                where d.IconTexture() != null
                select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsBuildingX".Translate(d.LabelCap)));
            list.AddRange(from d in research.getRecipesUnlocked()
                where d.IconTexture() != null
                select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsCraftingX".Translate(d.LabelCap)));
            list.AddRange(from d in research.getPlantsUnlocked()
                where d.IconTexture() != null
                select new Pair<Def, string>(d, "Fluffy.ResearchTree.AllowsPlantingX".Translate(d.LabelCap)));
            var list2 = research.Descendants();
            if (dedupe && list2.Any())
            {
                var descendantUnlocks = research.Descendants().SelectMany(c =>
                        from u in c.GetUnlockDefsAndDescriptions(false)
                        select u.First).Distinct()
                    .ToList();
                list = list.Where(u => !descendantUnlocks.Contains(u.First)).ToList();
            }

            _unlocksCache.Add(research, list);
            return list;
        }

        public float ApparentPercent()
        {
            return Mathf.Clamp01(research.ProgressApparent / research.CostApparent);
        }

        public ResearchNode ResearchNode()
        {
            if (IsAnomalyResearch(research))
            {
                return null;
            }

            var researchNode = Tree.ResearchToNode(research) as ResearchNode;
            if (researchNode == null)
            {
                // It would be better to use warning instead of error. This is just a reminder.
                // eg: RimFridge_PowerFactorSetting def is hidden, but it is also finished.
                // So the patch of "ResearchManager.FinishProject" method will be executed to jump here.
                Logging.Warning($"Node for {research.LabelCap} not found. Was it intentionally hidden or locked?",
                    true);
            }

            return researchNode;
        }

        public bool IsAnomalyResearch()
        {
            return ModsConfig.AnomalyActive && research.knowledgeCategory != null;
        }
    }
}