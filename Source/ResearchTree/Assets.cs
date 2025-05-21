// Assets.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[StaticConstructorOnStartup]
public static class Assets
{
    public static Thread initializeWorker;

    public static readonly Texture2D Button;

    public static readonly Texture2D ButtonActive;

    public static readonly Texture2D ResearchIcon;

    public static readonly Texture2D MoreIcon;

    public static readonly Texture2D Lock;

    internal static readonly Texture2D CircleFill;

    public static readonly Dictionary<TechLevel, Color> ColorCompleted;

    public static readonly Dictionary<TechLevel, Color> ColorAvailable;

    public static readonly Dictionary<TechLevel, Color> ColorUnavailable;

    public static Color TechLevelColor;

    public static bool RefreshResearch;

    public static int TotalAmountOfResearch;

    public static readonly bool UsingVanillaVehiclesExpanded;

    public static readonly bool UsingRimedieval;

    public static readonly bool UsingSOS2;

    public static readonly bool UsingMinimap;

    public static readonly bool UsingMedievalOverhaul;

    public static readonly bool UsingGrimworld;

    public static TechLevel CachedWorldTechLevel;

    public static readonly MethodInfo MedievalOverhaulPostfixMethod;

    public static readonly FieldInfo MedievalOverhaulSchematicDefField;

    public static readonly MethodInfo GrimworldPostfixMethod;

    public static readonly MethodInfo GrimworldInfoMethod;

    public static readonly MethodInfo IsDisabledMethod;

    public static readonly MethodInfo GetAllowedProjectDefsMethod;

    public static readonly PropertyInfo Sos2WorldCompPropertyInfo;

    public static readonly FieldInfo Sos2UlocksFieldInfo;

    public static readonly List<ResearchProjectDef> AllowedResearchDefs;

    public static readonly bool BetterResearchTabLoaded;

    public static readonly bool SemiRandomResearchLoaded;

    public static readonly MainButtonDef BetterResearchTab;

    public static readonly bool OrganizedResearchTabLoaded;

    public static readonly MainButtonDef OrganizedResearchTab;

    public static readonly Texture2D SemiRandomTexture2D;

    public static readonly object SettingsInstance;

    public static bool SemiResearchEnabled;

    static Assets()
    {
        if (ModLister.GetActiveModWithIdentifier("andery233xj.mod.BetterResearchTabs", true) != null)
        {
            BetterResearchTab = DefDatabase<MainButtonDef>.GetNamed("BetterResearchTab");
            BetterResearchTabLoaded = true;
        }

        if (ModLister.GetActiveModWithIdentifier("Mlie.OrganizedResearchTab", true) != null)
        {
            OrganizedResearchTab = DefDatabase<MainButtonDef>.GetNamed("OrganizedResearchTab");
            OrganizedResearchTabLoaded = true;
        }

        if (ModLister.GetActiveModWithIdentifier("CaptainMuscles.SemiRandomResearch.unofficial", true) != null)
        {
            SemiRandomTexture2D =
                ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_ResearchTree");
            new Harmony("Mlie.ResearchTree").Patch(
                AccessTools.Method("CM_Semi_Random_Research.MainTabWindow_NextResearch:DrawGoToTechTreeButton"),
                new HarmonyMethod(SemiRandomResearch_DrawGoToTechTreeButton.Prefix));
            SemiRandomResearchLoaded = true;
            SettingsInstance = AccessTools.Field("CM_Semi_Random_Research.SemiRandomResearchMod:settings")
                .GetValue(null);
            SemiResearchEnabled = (bool)AccessTools
                .Field("CM_Semi_Random_Research.SemiRandomResearchModSettings:featureEnabled")
                .GetValue(SettingsInstance);
        }

        if (ModLister.GetActiveModWithIdentifier("arodoid.semirandomprogression", true) != null)
        {
            SemiRandomTexture2D =
                ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_ResearchTree");
            new Harmony("Mlie.ResearchTree").Patch(
                AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText),
                    [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor)]),
                new HarmonyMethod(SemiRandomResearchProgressionFork_ButtonText.Prefix));
            SemiRandomResearchLoaded = true;
            SettingsInstance = AccessTools.Field("CM_Semi_Random_Research.SemiRandomResearchMod:settings")
                .GetValue(null);
            SemiResearchEnabled = (bool)AccessTools
                .Field("CM_Semi_Random_Research.SemiRandomResearchModSettings:featureEnabled")
                .GetValue(SettingsInstance);
        }

        Button = ContentFinder<Texture2D>.Get("Buttons/button");
        ButtonActive = ContentFinder<Texture2D>.Get("Buttons/button-active");
        ResearchIcon = ContentFinder<Texture2D>.Get("Icons/Research");
        MoreIcon = ContentFinder<Texture2D>.Get("Icons/more");
        Lock = ContentFinder<Texture2D>.Get("Icons/padlock");
        CircleFill = ContentFinder<Texture2D>.Get("Icons/circle-fill");
        ColorCompleted = new Dictionary<TechLevel, Color>();
        ColorAvailable = new Dictionary<TechLevel, Color>();
        ColorUnavailable = new Dictionary<TechLevel, Color>();
        TechLevelColor = new Color(1f, 1f, 1f, 0.2f);
        AllowedResearchDefs = [];

        UsingRimedieval =
            ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval") != null;
        if (UsingRimedieval)
        {
            var defCleanerType = AccessTools.TypeByName("Rimedieval.DefCleaner");
            if (defCleanerType == null)
            {
                Logging.Warning(
                    "Failed to find the DefCleaner-type in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                UsingRimedieval = false;
            }
            else
            {
                GetAllowedProjectDefsMethod = AccessTools.Method(defCleanerType, "GetAllowedProjectDefs",
                    [typeof(List<ResearchProjectDef>)]);
                if (GetAllowedProjectDefsMethod == null)
                {
                    Logging.Warning(
                        "Failed to find method GetAllowedProjectDefs in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                    UsingRimedieval = false;
                }
                else
                {
                    try
                    {
                        AllowedResearchDefs =
                            (List<ResearchProjectDef>)GetAllowedProjectDefsMethod.Invoke(null,
                            [
                                DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def =>
                                    !def.IsAnomalyResearch()).ToList()
                            ]);
                    }
                    catch (TargetInvocationException e)
                    {
                        Logging.Warning(
                            "Failed to get allowed research defs from Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                        Logging.Warning(e.InnerException?.Message);
                        UsingRimedieval = false;
                    }
                }
            }
        }

        UsingVanillaVehiclesExpanded =
            ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaVehiclesExpanded") != null;
        if (UsingVanillaVehiclesExpanded)
        {
            var utilsType = AccessTools.TypeByName("VanillaVehiclesExpanded.Utils");
            if (utilsType == null)
            {
                Logging.Warning(
                    "Failed to find the Utils-type in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                UsingVanillaVehiclesExpanded = false;
            }
            else
            {
                var utilsMethods = AccessTools.GetDeclaredMethods(utilsType);
                if (utilsMethods == null || !utilsMethods.Any())
                {
                    Logging.Warning(
                        "Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                    UsingVanillaVehiclesExpanded = false;
                }
                else
                {
                    IsDisabledMethod =
                        utilsMethods.FirstOrDefault(methodInfo => methodInfo.GetParameters().Length == 2);
                    if (IsDisabledMethod == null)
                    {
                        Logging.Warning(
                            "Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                        UsingVanillaVehiclesExpanded = false;
                    }
                }
            }
        }

        UsingMinimap =
            ModLister.GetActiveModWithIdentifier("dubwise.dubsmintminimap") != null;

        UsingMedievalOverhaul =
            ModLister.GetActiveModWithIdentifier("DankPyon.Medieval.Overhaul") != null;

        if (UsingMedievalOverhaul)
        {
            MedievalOverhaulPostfixMethod =
                AccessTools.Method("ResearchProjectDef_CanStartNow:Postfix");
            if (MedievalOverhaulPostfixMethod == null)
            {
                Logging.Warning(
                    "Failed to find the ResearchProjectDef_CanStartNow-PostFix-method in MedievalOverhaul. Will not be able to show or block research based on their extra requirements.");
                UsingMedievalOverhaul = false;
            }
            else
            {
                MedievalOverhaulSchematicDefField =
                    AccessTools.Field("MedievalOverhaul.RequiredSchematic:schematicDef");
                if (MedievalOverhaulSchematicDefField == null)
                {
                    Logging.Warning(
                        "Failed to find the RequiredSchematic-schematicDef-field in MedievalOverhaul. Will not be able to show or block research based on their extra requirements.");
                    UsingMedievalOverhaul = false;
                }
            }
        }

        UsingGrimworld =
            ModLister.GetActiveModWithIdentifier("Grimworld.Framework") != null;

        if (UsingGrimworld)
        {
            GrimworldPostfixMethod =
                AccessTools.Method("GW_Frame.HarmonyPatches:PrerequisitesCompletedPostFix");
            if (GrimworldPostfixMethod == null)
            {
                Logging.Warning(
                    "Failed to find the PrerequisitesCompletedPostFix-method in Grimworld 40K. Will not be able to show or block research based on their extra requirements.");
                UsingGrimworld = false;
            }

            GrimworldInfoMethod =
                AccessTools.Method("GW_Frame.HarmonyPatches:DrawResearchPrerequisitesPrefix");
            if (GrimworldInfoMethod == null)
            {
                Logging.Warning(
                    "Failed to find the DrawResearchPrerequisitesPrefix-method in Grimworld 40K. Will not be able to show or block research based on their extra requirements.");
                UsingGrimworld = false;
            }
        }

        UsingSOS2 =
            ModLister.GetActiveModWithIdentifier("kentington.saveourship2") != null;
        if (UsingSOS2)
        {
            var shipInteriorType = AccessTools.TypeByName("SaveOurShip2.ShipInteriorMod2");
            if (shipInteriorType == null)
            {
                Logging.Warning(
                    "Failed to find the ShipInteriorType-type in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                UsingSOS2 = false;
            }
            else
            {
                Sos2WorldCompPropertyInfo = AccessTools.Property(shipInteriorType, "WorldComp");
                if (Sos2WorldCompPropertyInfo == null)
                {
                    Logging.Warning(
                        "Failed to find method ShipWorldComp in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                    UsingSOS2 = false;
                }
                else
                {
                    var shipWorldCompType = AccessTools.TypeByName("SaveOurShip2.ShipWorldComp");
                    if (shipWorldCompType == null)
                    {
                        Logging.Warning(
                            "Failed to find type shipWorldCompType in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                        UsingSOS2 = false;
                    }
                    else
                    {
                        Sos2UlocksFieldInfo = AccessTools.Field(shipWorldCompType, "Unlocks");
                        if (Sos2UlocksFieldInfo == null)
                        {
                            Logging.Warning(
                                "Failed to find field Sos2UlocksFieldInfo in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                            UsingSOS2 = false;
                        }
                    }
                }
            }
        }

        var relevantTechLevels = Tree.RelevantTechLevels;
        var count = relevantTechLevels.Count;
        for (var i = 0; i < count; i++)
        {
            ColorCompleted[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.75f, 0.75f);
            ColorAvailable[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.33f, 0.33f);
            ColorUnavailable[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.125f, 0.33f);
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeLoadInBackground)
        {
            StartLoadingWorker();
        }
    }


    public static void StartLoadingWorker()
    {
        initializeWorker = new Thread(Tree.Initialize);
        Logging.Message("Initialization start in background");
        initializeWorker.Start();
    }

    public static bool IsBlockedByMedievalOverhaul(ResearchProjectDef researchProject)
    {
        if (!UsingMedievalOverhaul)
        {
            return false;
        }

        if (researchProject.modExtensions?.Any() == false)
        {
            return false;
        }

        var canStart = true;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse, will be updated by the postfix method
        var parameters = new object[] { researchProject, canStart };

        MedievalOverhaulPostfixMethod.Invoke(null, parameters);

        return !(bool)parameters[1];
    }

    public static bool TryGetBlockingSchematicFromMedievalOverhaul(ResearchProjectDef researchProject,
        out string thingLabel)
    {
        thingLabel = null;
        if (!UsingMedievalOverhaul)
        {
            return false;
        }

        if (researchProject.modExtensions?.Any() == false)
        {
            return false;
        }

        var modExtension =
            researchProject.modExtensions.FirstOrDefault(extension => extension.GetType().Name == "RequiredSchematic");
        if (modExtension == null)
        {
            return false;
        }

        var thingDef = (ThingDef)MedievalOverhaulSchematicDefField.GetValue(modExtension);
        if (thingDef == null)
        {
            return false;
        }

        thingLabel = thingDef.LabelCap;
        return true;
    }

    public static bool IsBlockedByGrimworld(ResearchProjectDef researchProject)
    {
        if (!UsingGrimworld)
        {
            return false;
        }

        if (researchProject.modExtensions?.Any() == false)
        {
            return false;
        }

        var canStart = true;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse, will be updated by the postfix method
        var parameters = new object[] { canStart, researchProject };

        GrimworldPostfixMethod.Invoke(null, parameters);

        return !(bool)parameters[0];
    }

    public static bool IsBlockedBySOS2(ResearchProjectDef researchProject)
    {
        if (!UsingSOS2)
        {
            return false;
        }

        if (researchProject.tab is not { defName: "ResearchTabArchotech" })
        {
            return false;
        }

        var worldComp = Sos2WorldCompPropertyInfo.GetValue(null, null);
        if (worldComp == null)
        {
            return false;
        }

        var unlocks = (List<string>)Sos2UlocksFieldInfo.GetValue(worldComp);
        if (unlocks == null)
        {
            return false;
        }

        if (!unlocks.Any())
        {
            return true;
        }

        return !unlocks.Contains("ArchotechUplink");
    }

    public static void DrawWindowBackground(Rect rect, Color bgColor)
    {
        GUI.color = Widgets.WindowBGFillColor;
        GUI.DrawTexture(rect, BaseContent.WhiteTex);
        GUI.color = bgColor;
        GUI.DrawTexture(rect, BaseContent.WhiteTex);
        GUI.color = Widgets.WindowBGBorderColor;
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
    }


    [StaticConstructorOnStartup]
    public static class Lines
    {
        public static readonly Texture2D Circle = ContentFinder<Texture2D>.Get("Lines/Outline/circle");

        public static readonly Texture2D End = ContentFinder<Texture2D>.Get("Lines/Outline/end");

        public static readonly Texture2D EW = ContentFinder<Texture2D>.Get("Lines/Outline/ew");

        public static readonly Texture2D NS = ContentFinder<Texture2D>.Get("Lines/Outline/ns");
    }


    [DefOf]
    public static class MainButtonDefOf
    {
        public static MainButtonDef FluffyResearchTree;
    }
}