// Node.cs
// Copyright Karel Kroeze, 2019-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class Node
{
    protected const float Offset = 2f;

    protected readonly List<Edge<Node, Node>> _inEdges = [];

    protected readonly List<Edge<Node, Node>> _outEdges = [];

    protected Rect _costIconRect;

    protected Rect _costLabelRect;

    protected Rect _iconsRect;

    protected Rect _labelRect;

    protected bool _largeLabel;

    protected Vector2 _left = Vector2.zero;

    protected Rect _lockRect;

    protected Vector2 _pos = Vector2.zero;

    protected Rect _rect;

    protected bool _rectsSet;

    protected Vector2 _right = Vector2.zero;

    protected Vector2 _topLeft = Vector2.zero;

    public List<Node> Descendants => OutNodes.Concat(OutNodes.SelectMany(n => n.Descendants)).ToList();

    public List<Edge<Node, Node>> OutEdges => _outEdges;

    public List<Node> OutNodes => _outEdges.Select(e => e.Out).ToList();

    public List<Edge<Node, Node>> InEdges => _inEdges;

    public List<Node> InNodes => _inEdges.Select(e => e.In).ToList();

    public List<Edge<Node, Node>> Edges => _inEdges.Concat(_outEdges).ToList();

    public List<Node> Nodes => InNodes.Concat(OutNodes).ToList();

    protected Rect CostIconRect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _costIconRect;
        }
    }

    protected Rect CostLabelRect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _costLabelRect;
        }
    }

    public virtual Color Color => Color.white;

    public virtual Color EdgeColor => Color;

    protected Rect IconsRect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _iconsRect;
        }
    }

    protected Rect LabelRect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _labelRect;
        }
    }

    public Vector2 Left
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _left;
        }
    }

    public Rect QueueRect { get; set; }

    public Rect LockRect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _lockRect;
        }
    }

    protected internal Rect Rect
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _rect;
        }
    }

    public Vector2 Right
    {
        get
        {
            if (!_rectsSet)
            {
                SetRects();
            }

            return _right;
        }
    }

    public Vector2 Center => (Left + Right) / Offset;

    public virtual int X
    {
        get => (int)_pos.x;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (Math.Abs(_pos.x - value) < Constants.Epsilon)
            {
                return;
            }

            _pos.x = value;
            _rectsSet = false;
            Tree.Size.x = Tree.Nodes.Max(n => n.X);
            Tree.OrderDirty = true;
        }
    }

    public virtual int Y
    {
        get => (int)_pos.y;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (Math.Abs(_pos.y - value) < Constants.Epsilon)
            {
                return;
            }

            _pos.y = value;
            _rectsSet = false;
            Tree.Size.z = Tree.Nodes.Max(n => n.Y);
            Tree.OrderDirty = true;
        }
    }

    public virtual Vector2 Pos => new Vector2(X, Y);

    public virtual float Yf
    {
        get => _pos.y;
        set
        {
            if (Math.Abs(_pos.y - value) < Constants.Epsilon)
            {
                return;
            }

            _pos.y = value;
            Tree.Size.z = Tree.Nodes.Max(n => n.Y) + 1;
            Tree.OrderDirty = true;
        }
    }

    public virtual string Label { get; }

    public virtual bool Completed => false;

    public virtual bool Available => false;

    public virtual bool Highlighted { get; set; }

    protected internal virtual bool SetDepth(int min = 1)
    {
        var num = Mathf.Max(InNodes.NullOrEmpty() ? 1 : InNodes.Max(n => n.X) + 1, min);
        if (num == X)
        {
            return false;
        }

        X = num;
        return true;
    }

    public virtual void Debug()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"{Label} ({X}, {Y}):");
        stringBuilder.AppendLine("- Parents");
        foreach (var inNode in InNodes)
        {
            stringBuilder.AppendLine($"-- {inNode.Label}");
        }

        stringBuilder.AppendLine("- Children");
        foreach (var outNode in OutNodes)
        {
            stringBuilder.AppendLine($"-- {outNode.Label}");
        }

        stringBuilder.AppendLine("");
        Logging.Message(stringBuilder.ToString());
    }

    public override string ToString()
    {
        var label = Label;
        var pos = _pos;
        return label + pos;
    }

    protected void SetRects()
    {
        // origin
        _topLeft = new Vector2(
            (X - 1) * (Constants.NodeSize.x + Constants.NodeMargins.x),
            (Yf - 1) * (Constants.NodeSize.y + Constants.NodeMargins.y));

        SetRects(_topLeft);
    }

    protected void SetRects(Vector2 topLeft)
    {
        // main rect
        _rect = new Rect(topLeft.x,
            topLeft.y,
            Constants.NodeSize.x,
            Constants.NodeSize.y);

        // left and right edges
        _left = new Vector2(_rect.xMin, _rect.yMin + (_rect.height / 2f));
        _right = new Vector2(_rect.xMax, _left.y);

        // label rect
        _labelRect = new Rect(_rect.xMin + 6f,
            _rect.yMin + 3f,
            (_rect.width * 2f / 3f) - 6f,
            (_rect.height * .5f) - 3f);

        // research cost rect
        _costLabelRect = new Rect(_rect.xMin + (_rect.width * 2f / 3f),
            _rect.yMin + 3f,
            (_rect.width * 1f / 3f) - 16f - 3f,
            (_rect.height * .5f) - 3f);

        // research icon rect
        _costIconRect = new Rect(_costLabelRect.xMax,
            _rect.yMin + ((_costLabelRect.height - 16f) / 2),
            16f,
            16f);

        // icon container rect
        _iconsRect = new Rect(_rect.xMin,
            _rect.yMin + (_rect.height * .5f),
            _rect.width,
            _rect.height * .5f);

        // lock icon rect
        _lockRect = new Rect(0f, 0f, 32f, 32f);
        _lockRect = _lockRect.CenteredOnXIn(_rect);
        _lockRect = _lockRect.CenteredOnYIn(_rect);

        // see if the label is too big
        _largeLabel = Text.CalcHeight(Label, _labelRect.width) > _labelRect.height;

        // done
        _rectsSet = true;
    }

    public virtual bool IsVisible(Rect visibleRect)
    {
        if (!(Rect.xMin > visibleRect.xMax) && !(Rect.xMax < visibleRect.xMin) && !(Rect.yMin > visibleRect.yMax))
        {
            return !(Rect.yMax < visibleRect.yMin);
        }

        return false;
    }

    public virtual void Draw(Rect visibleRect, bool forceDetailedMode = false, bool drawLabel = true)
    {
    }
}