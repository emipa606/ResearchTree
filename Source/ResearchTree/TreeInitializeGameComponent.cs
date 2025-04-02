﻿using Verse;

namespace FluffyResearchTree;

public class TreeInitializeGameComponent : GameComponent
{
    public TreeInitializeGameComponent() {} 
    
    public TreeInitializeGameComponent(Game game) {} 
        
    public override void FinalizeInit()
    {
        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeLoadInBackground)
        {
            LongEventHandler.QueueLongEvent(Tree.Initialize, "ResearchPal.BuildingResearchTreeAsync", true, null);
        }
    }
}