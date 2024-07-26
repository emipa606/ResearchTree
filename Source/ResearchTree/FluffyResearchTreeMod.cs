﻿using System.Reflection;
using HarmonyLib;
using Mlie;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

internal class FluffyResearchTreeMod : Mod
{
    /// <summary>
    ///     The instance of the settings to be read by the mod
    /// </summary>
    public static FluffyResearchTreeMod instance;

    private static string currentVersion;

    //public static Thread initializeWorker;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="content"></param>
    public FluffyResearchTreeMod(ModContentPack content) : base(content)
    {
        instance = this;
        new Harmony("Fluffy.ResearchTree").PatchAll(Assembly.GetExecutingAssembly());
        Settings = GetSettings<FluffyResearchTreeSettings>();
        currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);
    }

    /// <summary>
    ///     The instance-settings for the mod
    /// </summary>
    internal FluffyResearchTreeSettings Settings { get; }

    public override string SettingsCategory()
    {
        return "Research Tree";
    }

    /// <summary>
    ///     The settings-window
    ///     For more info: https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings
    /// </summary>
    /// <param name="rect"></param>
    public override void DoSettingsWindowContents(Rect rect)
    {
        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(rect);
        listing_Standard.Gap();
        listing_Standard.Label("Fluffy.ResearchTree.LoadType".Translate());

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeTwo".Translate(), Settings.LoadType == 1))
        {
            Settings.LoadType = 1;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeThree".Translate(), Settings.LoadType == 2))
        {
            Settings.LoadType = 2;
        }

        if (Settings.LoadType == 1 && Prefs.UIScale > 1f)
        {
            listing_Standard.Label("Fluffy.ResearchTree.UIScaleWarning".Translate());
        }

        listing_Standard.Gap();

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.CtrlFunctionScroll".Translate(), Settings.CtrlFunction))
        {
            Settings.CtrlFunction = true;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.CtrlFunctionZoom".Translate(), !Settings.CtrlFunction))
        {
            Settings.CtrlFunction = false;
        }

        listing_Standard.Gap();
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.OverrideResearch".Translate(),
            ref Settings.OverrideResearch);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.PauseOnOpen".Translate(), ref Settings.PauseOnOpen);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.ShowCompletion".Translate(), ref Settings.ShowCompletion);
        //listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.VanillaGraphics".Translate(),
        //    ref Settings.VanillaGraphics);

        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("Fluffy.ResearchTree.CurrentModVersion".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.End();
    }
}