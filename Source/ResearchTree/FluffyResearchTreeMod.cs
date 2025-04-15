using System.Reflection;
using ColourPicker;
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

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeTwo".Translate(),
                Settings.LoadType == Constants.LoadTypeLoadInBackground))
        {
            Settings.LoadType = Constants.LoadTypeLoadInBackground;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeThree".Translate(),
                Settings.LoadType == Constants.LoadTypeFirstTimeOpening))
        {
            Settings.LoadType = Constants.LoadTypeFirstTimeOpening;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeFour".Translate(), Settings.DoNotGenerateResearchTree()))
        {
            Settings.LoadType = Constants.LoadTypeDoNotGenerateResearchTree;
            Settings.OverrideResearch = false;
        }

        if (Settings.LoadType == Constants.LoadTypeLoadInBackground && Prefs.UIScale > 1f)
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
        if (Settings.OverrideResearch && Settings.DoNotGenerateResearchTree())
        {
            Settings.LoadType = Constants.LoadTypeLoadInBackground;
        }

        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.PauseOnOpen".Translate(), ref Settings.PauseOnOpen);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.ShowCompletion".Translate(), ref Settings.ShowCompletion);
        if (ModsConfig.IdeologyActive)
        {
            listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.NoIdeologyPopup".Translate(),
                ref Settings.NoIdeologyPopup, "Fluffy.ResearchTree.NoIdeologyPopupTT".Translate());
        }
        else
        {
            Settings.NoIdeologyPopup = false;
        }

        var colorRect = listing_Standard.GetRect(30f);
        Widgets.Label(colorRect.LeftHalf(), "Fluffy.ResearchTree.BackgroundColor".Translate());
        Widgets.DrawBoxSolidWithOutline(colorRect.RightHalf().RightHalf(), Settings.BackgroundColor,
            Widgets.WindowBGBorderColor, 2);
        if (Widgets.ButtonInvisible(colorRect.RightHalf().RightHalf()))
        {
            Find.WindowStack.Add(new Dialog_ColourPicker(Settings.BackgroundColor,
                color => { Settings.BackgroundColor = color; }));
        }

        listing_Standard.GapLine();
        if (listing_Standard.ButtonTextLabeledPct("Fluffy.ResearchTree.ResetLabel".Translate(),
                "Fluffy.ResearchTree.Reset".Translate(), 0.75f))
        {
            Settings.Reset();
        }

        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.VerboseLogging".Translate(), ref Settings.VerboseLogging);

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