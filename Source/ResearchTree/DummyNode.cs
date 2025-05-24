// DummyNode.cs
// Copyright Karel Kroeze, 2018-2020

using System.Linq;
using UnityEngine;

namespace FluffyResearchTree;

public class DummyNode : Node
{
    public override string Label => $"DUMMY: {Parent?.Label ?? "??"} -> {Child?.Label ?? "??"}";

    public ResearchNode Parent
    {
        get
        {
            if (InNodes.FirstOrDefault() is ResearchNode result)
            {
                return result;
            }

            return (InNodes.FirstOrDefault() as DummyNode)?.Parent;
        }
    }

    public ResearchNode Child
    {
        get
        {
            if (OutNodes.FirstOrDefault() is ResearchNode result)
            {
                return result;
            }

            return (OutNodes.FirstOrDefault() as DummyNode)?.Child;
        }
    }

    public override bool Completed => OutNodes.FirstOrDefault()?.Completed ?? false;

    public override bool Available => OutNodes.FirstOrDefault()?.Available ?? false;

    public override bool Highlighted => OutNodes.FirstOrDefault()?.Highlighted ?? false;

    public override Color Color => OutNodes.FirstOrDefault()?.Color ?? Color.white;

    public override Color EdgeColor => OutNodes.FirstOrDefault()?.EdgeColor ?? Color.white;

    public override bool IsVisible => OutNodes.FirstOrDefault()?.IsVisible ?? false;
}