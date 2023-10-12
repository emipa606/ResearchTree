// Assets.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[StaticConstructorOnStartup]
public static class Assets
{
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

    public static readonly Texture2D SlightlyDarkBackground;

    public static readonly Texture2D Search;

    public static bool RefreshResearch;

    public static int TotalAmountOfResearch;

    public static readonly bool UsingVanillaVehiclesExpanded;

    public static MethodInfo IsDisabledMethod;

    static Assets()
    {
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
        SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture(0f, 0f, 0f, 0.1f);
        Search = ContentFinder<Texture2D>.Get("Icons/magnifying-glass");
        UsingVanillaVehiclesExpanded =
            ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaVehiclesExpanded") != null;

        if (UsingVanillaVehiclesExpanded)
        {
            var utilsType = AccessTools.TypeByName("VanillaVehiclesExpanded.Utils");
            if (utilsType == null)
            {
                Log.Warning(
                    "[FluffyResearchTree]: Failed to find the Utils-type in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                UsingVanillaVehiclesExpanded = false;
            }
            else
            {
                var utilsMethods = AccessTools.GetDeclaredMethods(utilsType);
                if (utilsMethods == null || !utilsMethods.Any())
                {
                    Log.Warning(
                        "[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                    UsingVanillaVehiclesExpanded = false;
                }
                else
                {
                    IsDisabledMethod =
                        utilsMethods.FirstOrDefault(methodInfo => methodInfo.GetParameters().Length == 2);
                    if (IsDisabledMethod == null)
                    {
                        Log.Warning(
                            "[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                        UsingVanillaVehiclesExpanded = false;
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
        public static MainButtonDef ResearchOriginal;
    }
}