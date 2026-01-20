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
    private static Thread initializeWorker;

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

    private static readonly bool UsingSOS2;

    public static readonly bool UsingMinimap;

    private static readonly bool UsingMedievalOverhaul;

    public static readonly bool UsingWorldTechLevel;

    private static readonly bool UsingGrimworld;

    private static readonly MethodInfo WorldTechLevelProjectVisibleMethod;

    private static readonly MethodInfo WorldTechLevelSectionVisibleMethod;

    private static readonly MethodInfo MedievalOverhaulPostfixMethod;

    private static readonly FieldInfo MedievalOverhaulSchematicDefField;

    private static readonly MethodInfo GrimworldPostfixMethod;

    public static readonly MethodInfo GrimworldInfoMethod;

    public static readonly MethodInfo IsDisabledMethod;

    private static readonly PropertyInfo Sos2WorldCompPropertyInfo;

    private static readonly FieldInfo Sos2UlocksFieldInfo;

    public static readonly List<ResearchProjectDef> RimedievalAllowedResearchDefs;

    private static readonly TechLevel RimedievalMaxTechLevel;

    public static readonly bool BetterResearchTabLoaded;

    public static readonly bool SemiRandomResearchLoaded;

    public static readonly MainButtonDef BetterResearchTab;

    public static readonly bool OrganizedResearchTabLoaded;

    public static readonly MainButtonDef OrganizedResearchTab;

    public static readonly Texture2D SemiRandomTexture2D;

    public static readonly object SettingsInstance;

    public static bool SemiResearchEnabled;

    public static Color WindowBgBorderColor = AccessTools.Field(typeof(Widgets), "WindowBGBorderColor")
        .GetValue(null) as Color? ?? new Color(0.2f, 0.2f, 0.2f);

    public static readonly MethodInfo InternalDrawTextureMethod =
        AccessTools.Method(typeof(Graphics), "Internal_DrawTexture");

    private static readonly PropertyInfo roundedRectMaterialProperty =
        AccessTools.Property(typeof(GUI), "roundedRectMaterial");

    static Assets()
    {
        var harmonyPatcher = new Harmony("Mlie.ResearchTree");
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
            harmonyPatcher.Patch(
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
            harmonyPatcher.Patch(
                AccessTools.Method("CM_Semi_Random_Research.MainTabWindow_NextResearch:DrawLeftColumn"), postfix:
                new HarmonyMethod(SemiRandomResearch_DrawGoToTechTreeButton.ProgressionPostfix));
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
        RimedievalAllowedResearchDefs = [];

        UsingRimedieval = ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval", true) != null;
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
                var getAllowedProjectDefsMethod1 = AccessTools.Method(defCleanerType, "GetAllowedProjectDefs",
                    [typeof(List<ResearchProjectDef>)]);
                if (getAllowedProjectDefsMethod1 == null)
                {
                    Logging.Warning(
                        "Failed to find method GetAllowedProjectDefs in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                    UsingRimedieval = false;
                }
                else
                {
                    try
                    {
                        RimedievalAllowedResearchDefs =
                            (List<ResearchProjectDef>)getAllowedProjectDefsMethod1.Invoke(null,
                            [
                                DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def =>
                                    !def.IsAnomalyResearch()).ToList()
                            ]);

                        RimedievalMaxTechLevel = RimedievalAllowedResearchDefs.Max(e => e.techLevel);
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
            ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaVehiclesExpanded", true) != null;
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

        UsingMinimap = ModLister.GetActiveModWithIdentifier("dubwise.dubsmintminimap", true) != null;

        UsingWorldTechLevel = ModLister.GetActiveModWithIdentifier("m00nl1ght.WorldTechLevel", true) != null;

        if (UsingWorldTechLevel)
        {
            WorldTechLevelProjectVisibleMethod =
                AccessTools.Method("WorldTechLevel.ResearchUtility:ShouldProjectBeVisible");
            if (WorldTechLevelProjectVisibleMethod == null)
            {
                Logging.Warning(
                    "Failed to find the ResearchUtility-ShouldProjectBeVisible-method in WorldTechLevel. Will not be able to hide research based on their extra requirements.");
                UsingWorldTechLevel = false;
            }
            else
            {
                WorldTechLevelSectionVisibleMethod =
                    AccessTools.Method("WorldTechLevel.ResearchUtility:ShouldSectionBeVisible");
                if (WorldTechLevelSectionVisibleMethod == null)
                {
                    Logging.Warning(
                        "Failed to find the ResearchUtility-ShouldSectionBeVisible-method in WorldTechLevel. Will not be able to hide research based on their extra requirements.");
                    UsingWorldTechLevel = false;
                }
            }
        }

        UsingMedievalOverhaul = ModLister.GetActiveModWithIdentifier("DankPyon.Medieval.Overhaul", true) != null;

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

        UsingGrimworld = ModLister.GetActiveModWithIdentifier("Grimworld.Framework", true) != null;

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

        UsingSOS2 = ModLister.GetActiveModWithIdentifier("kentington.saveourship2", true) != null;
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

    public static Material RoundedRectMaterial
    {
        get
        {
            if (roundedRectMaterialProperty == null)
            {
                return null;
            }

            if (field != null)
            {
                return field;
            }

            field = (Material)roundedRectMaterialProperty.GetValue(null, null);
            return field;
        }
    }


    private static void StartLoadingWorker()
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

    public static bool IsBlockedByWorldTechLevel(ResearchProjectDef researchProject)
    {
        if (!UsingWorldTechLevel)
        {
            return false;
        }

        return !(bool)WorldTechLevelProjectVisibleMethod.Invoke(null, [researchProject]);
    }

    public static bool IsHiddenByTechLevelRestrictions(ResearchProjectDef researchProject)
    {
        if (!FluffyResearchTreeMod.instance.Settings.HideNodesBlockedByTechLevel)
        {
            return false;
        }

        if (UsingWorldTechLevel && !(bool)WorldTechLevelProjectVisibleMethod.Invoke(null, [researchProject]))
        {
            return true;
        }

        return UsingRimedieval && !RimedievalAllowedResearchDefs.Contains(researchProject);
    }

    public static bool IsHiddenByTechLevelRestrictions(TechLevel techLevel)
    {
        if (!FluffyResearchTreeMod.instance.Settings.HideNodesBlockedByTechLevel)
        {
            return false;
        }

        if (UsingWorldTechLevel && !(bool)WorldTechLevelSectionVisibleMethod.Invoke(null, [techLevel]))
        {
            return true;
        }

        return UsingRimedieval && techLevel > RimedievalMaxTechLevel;
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
        GUI.color = WindowBgBorderColor;
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