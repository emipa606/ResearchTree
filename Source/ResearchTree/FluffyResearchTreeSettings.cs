using UnityEngine;
using Verse;

namespace FluffyResearchTree;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class FluffyResearchTreeSettings : ModSettings
{
    public Color BackgroundColor = new(0f, 0f, 0f, 0.1f);
    public bool CtrlFunction = true;
    public bool HideNodesBlockedByTechLevel;
    public int LoadType = Constants.LoadTypeLoadInBackground;
    public bool NoIdeologyPopup;
    public bool OverrideResearch = true;
    public bool PauseOnOpen = true;
    public bool ReverseShift;
    public float ScrollSpeed = 1f;

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
        Scribe_Values.Look(ref ReverseShift, "ReverseShift");
        Scribe_Values.Look(ref ScrollSpeed, "ScrollSpeed", 1f);
        Scribe_Values.Look(ref NoIdeologyPopup, "NoIdeologyPopup");
        Scribe_Values.Look(ref HideNodesBlockedByTechLevel, "HideNodesBlockedByTechLevel");
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
        ReverseShift = false;
        NoIdeologyPopup = false;
        HideNodesBlockedByTechLevel = false;
        VerboseLogging = false;
        ScrollSpeed = 1f;
        LoadType = Constants.LoadTypeLoadInBackground;
        BackgroundColor = new Color(0f, 0f, 0f, 0.1f);
    }
}