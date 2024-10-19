﻿using UnityEngine;
using Verse;

namespace FluffyResearchTree;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class FluffyResearchTreeSettings : ModSettings
{
    public Color BackgroundColor = new Color(0f, 0f, 0f, 0.1f);
    public bool CtrlFunction = true;
    public int LoadType = 1;
    public bool NoIdeologyPopup;
    public bool OverrideResearch = true;
    public bool PauseOnOpen = true;

    public bool ShowCompletion;

    public bool VerboseLogging;

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref PauseOnOpen, "PauseOnOpen", true);
        Scribe_Values.Look(ref CtrlFunction, "CtrlFunction", true);
        Scribe_Values.Look(ref OverrideResearch, "OverrideResearch", true);
        Scribe_Values.Look(ref ShowCompletion, "ShowCompletion");
        Scribe_Values.Look(ref NoIdeologyPopup, "NoIdeologyPopup");
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref LoadType, "LoadType", 1);
        Scribe_Values.Look(ref BackgroundColor, "BackgroundColor", new Color(0f, 0f, 0f, 0.1f));
    }

    public void Reset()
    {
        PauseOnOpen = true;
        CtrlFunction = true;
        OverrideResearch = true;
        ShowCompletion = false;
        NoIdeologyPopup = false;
        VerboseLogging = false;
        LoadType = 1;
        BackgroundColor = new Color(0f, 0f, 0f, 0.1f);
    }
}