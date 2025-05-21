// Edge.cs
// Copyright Karel Kroeze, 2018-2020

using System;
using UnityEngine;

namespace FluffyResearchTree;

public class Edge<T1, T2> where T1 : Node where T2 : Node
{
    private T1 _in;

    private T2 _out;

    public Edge(T1 @in, T2 @out)
    {
        _in = @in;
        _out = @out;
        IsDummy = _out is DummyNode;
    }

    public T1 In
    {
        get => _in;
        set
        {
            _in = value;
            IsDummy = _out is DummyNode;
        }
    }

    public T2 Out
    {
        get => _out;
        set
        {
            _out = value;
            IsDummy = _out is DummyNode;
        }
    }

    public int Span => _out.X - _in.X;

    public float Length => Mathf.Abs(_in.Yf - _out.Yf) * (!IsDummy ? 1 : 10);

    public bool IsDummy { get; private set; }

    public int DrawOrder
    {
        get
        {
            if (Out.Highlighted)
            {
                return 3;
            }

            if (Out.Completed)
            {
                return 2;
            }

            return Out.Available ? 1 : 0;
        }
    }

    public void Draw(Rect visibleRect)
    {
        if (!ShouldDrawEdge(visibleRect))
        {
            return;
        }

        var right = In.Right;
        var left = Out.Left;
        if (Math.Abs(right.y - left.y) < Constants.Epsilon)
        {
            FastGUI.DrawTextureFast(new Rect(right.x, right.y - 2f, left.x - right.x, 4f), Assets.Lines.EW,
                Out.EdgeColor);
        }
        else
        {
            var num = Math.Min(right.y, left.y) + (Constants.NodeMargins.x / 4f);
            var num2 = Math.Max(right.y, left.y) - (Constants.NodeMargins.x / 4f);
            FastGUI.DrawTextureFast(new Rect(right.x, right.y - 2f, Constants.NodeMargins.x / 4f, 4f), Assets.Lines.EW,
                Out.EdgeColor);
            FastGUI.DrawTextureFast(new Rect(right.x + (Constants.NodeMargins.x / 2f) - 2f, num, 4f, num2 - num),
                Assets.Lines.NS, Out.EdgeColor);
            FastGUI.DrawTextureFast(
                new Rect(right.x + (Constants.NodeMargins.x / 4f * 3f), left.y - 2f,
                    left.x - right.x - (Constants.NodeMargins.x / 4f * 3f), 4f), Assets.Lines.EW, Out.EdgeColor);
            var position = new Rect(right.x + (Constants.NodeMargins.x / 4f), right.y - (Constants.NodeMargins.x / 4f),
                Constants.NodeMargins.x / 2f, Constants.NodeMargins.x / 2f);
            var position2 = new Rect(right.x + (Constants.NodeMargins.x / 4f), left.y - (Constants.NodeMargins.x / 4f),
                Constants.NodeMargins.x / 2f, Constants.NodeMargins.x / 2f);
            if (right.y < left.y)
            {
                FastGUI.DrawTextureFastWithCoords(position, Assets.Lines.Circle,
                    new Rect(0.5f, 0.5f, 0.5f, 0.5f), Out.EdgeColor);
                FastGUI.DrawTextureFastWithCoords(position2, Assets.Lines.Circle,
                    new Rect(0f, 0f, 0.5f, 0.5f), Out.EdgeColor);
            }
            else
            {
                FastGUI.DrawTextureFastWithCoords(position, Assets.Lines.Circle,
                    new Rect(0.5f, 0f, 0.5f, 0.5f), Out.EdgeColor);
                FastGUI.DrawTextureFastWithCoords(position2, Assets.Lines.Circle,
                    new Rect(0f, 0.5f, 0.5f, 0.5f), Out.EdgeColor);
            }
        }

        if (!IsDummy)
        {
            FastGUI.DrawTextureFast(
                new Rect(left.x - Constants.HubSize, left.y - 8f, Constants.HubSize, Constants.HubSize),
                Assets.Lines.End, Out.EdgeColor);
        }
        else
        {
            FastGUI.DrawTextureFast(new Rect(left.x, left.y - 2f, Constants.NodeSize.x, 4f), Assets.Lines.EW,
                Out.EdgeColor);
        }
    }

    private bool ShouldDrawEdge(Rect visibleRect)
    {
        return GetEdgeInNode()?.IsVisible(visibleRect) == true &&
               GetEdgeOutNode()?.IsVisible(visibleRect) == true;
    }

    private Node GetEdgeInNode()
    {
        return In is DummyNode d ? d.Parent : In;
    }

    private Node GetEdgeOutNode()
    {
        return Out is DummyNode d ? d.Child : Out;
    }

    public override string ToString()
    {
        return $"{_in} -> {_out}";
    }
}