// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Shared._Lua.Achievements;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.Achievements;

public sealed class AchievementsTreeView : LayoutContainer
{
    public const int GridSize = 88;
    public const int Padding = 24;

    public float TopInset { get; set; } = 40f;

    public event Action<string>? OnNodeSelected;

    private readonly Dictionary<string, AchievementTreeNode> _nodes = new();
    private readonly List<(string From, string To)> _edges = new();
    private string? _selectedID;

    public AchievementsTreeView()
    {
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = false;
        InheritChildMeasure = false;
        MinSize = new Vector2(200, 160);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (ChildCount == 0)
            return MinSize;

        var maxX = 0f;
        var maxY = 0f;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var size = child.DesiredSize;
            if (size == Vector2.Zero)
                size = child.SetSize;

            maxX = Math.Max(maxX, child.Position.X + size.X);
            maxY = Math.Max(maxY, child.Position.Y + size.Y);
        }

        return new Vector2(maxX + Padding, maxY + Padding);
    }

    public void Rebuild(
        IEnumerable<AchievementEntry> entries,
        IPrototypeManager prototypes,
        SpriteSystem sprite,
        string? selectedId)
    {
        RemoveAllChildren();
        _nodes.Clear();
        _edges.Clear();
        _selectedID = selectedId;

        var unlocked = entries.Where(e => e.Unlocked).Select(e => e.AchievementId).ToHashSet();
        var visible = new List<(AchievementPrototype Proto, AchievementNodeState State)>();

        foreach (var proto in prototypes.EnumeratePrototypes<AchievementPrototype>())
        {
            var state = AchievementTreeLogic.GetState(proto, unlocked);
            if (state == AchievementNodeState.Hidden)
                continue;

            visible.Add((proto, state));
        }

        if (visible.Count == 0)
        {
            MinSize = new Vector2(200, 160);
            InvalidateMeasure();
            InvalidateArrange();
            return;
        }

        var depthCache = new Dictionary<string, int>();
        var autoPos = new Dictionary<int, int>();
        var entryById = entries.ToDictionary(e => e.AchievementId);

        foreach (var (proto, state) in visible.OrderBy(v => AchievementTreeLogic.GetDepth(v.Proto, prototypes, depthCache)).ThenBy(v => v.Proto.ID))
        {
            Vector2i grid;
            if (proto.Position is { } manual)
            {
                grid = manual;
            }
            else
            {
                var depth = AchievementTreeLogic.GetDepth(proto, prototypes, depthCache);
                autoPos.TryGetValue(depth, out var row);
                grid = new Vector2i(depth, row);
                autoPos[depth] = row + 1;
            }

            entryById.TryGetValue(proto.ID, out var entry);
            var progress = entry?.Progress ?? 0;
            var progressMax = entry?.ProgressMax ?? 0;

            var node = new AchievementTreeNode(
                proto.ID,
                AchievementJobText.GetIconLayers(proto, prototypes),
                sprite,
                state,
                progress,
                progressMax)
            {
                Pressed = proto.ID == _selectedID,
                ToolTip = AchievementJobText.GetName(proto, prototypes),
            };

            var id = proto.ID;
            node.OnPressed += _ =>
            {
                _selectedID = id;
                foreach (var n in _nodes.Values)
                    n.Pressed = n.AchievementId == _selectedID;
                OnNodeSelected?.Invoke(id);
            };

            var uiPos = new Vector2(Padding + grid.X * GridSize, TopInset + grid.Y * GridSize);
            SetPosition(node, uiPos);
            AddChild(node);
            _nodes[proto.ID] = node;
        }

        foreach (var (proto, _) in visible)
        {
            foreach (var prereq in proto.Prerequisites)
            {
                if (_nodes.ContainsKey(prereq) && _nodes.ContainsKey(proto.ID))
                    _edges.Add((prereq, proto.ID));
            }
        }

        var maxX = 0f;
        var maxY = 0f;
        foreach (var child in Children)
        {
            maxX = Math.Max(maxX, child.Position.X + child.SetSize.X);
            maxY = Math.Max(maxY, child.Position.Y + child.SetSize.Y);
        }

        var contentSize = new Vector2(maxX + Padding, maxY + Padding);
        MinSize = contentSize;
        SetSize = contentSize;
        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_edges.Count == 0)
            return;

        var nodeSize = AchievementTreeNode.NodeSize;
        var uiScale = UIScale;
        var ordered = _edges
            .Select(edge => (edge, related: IsRelatedEdge(edge.From, edge.To)))
            .OrderBy(t => t.related)
            .ToList();

        foreach (var (edge, related) in ordered)
        {
            if (!_nodes.TryGetValue(edge.From, out var from) || !_nodes.TryGetValue(edge.To, out var to))
                continue;

            var obstacles = BuildObstacleRects(edge.From, edge.To, nodeSize);
            var points = AchievementTreeEdgeRouting.BuildPath(from.Position, to.Position, nodeSize, obstacles);
            if (points.Count < 2)
                continue;

            var color = ResolveEdgeColor(from, to, related);
            var thicknessBoost = related ||
                                 (from.State == AchievementNodeState.Unlocked && to.State == AchievementNodeState.Available);

            for (var p = 0; p < points.Count - 1; p++)
            {
                var a = points[p] * uiScale;
                var b = points[p + 1] * uiScale;
                handle.DrawLine(a, b, color);

                if (thicknessBoost)
                {
                    var glow = color.WithAlpha(Math.Clamp(color.A * 0.45f, 0.1f, 0.4f));
                    handle.DrawLine(a + new Vector2(0f, uiScale), b + new Vector2(0f, uiScale), glow);
                }
            }
        }
    }

    private List<AchievementTreeEdgeRouting.Rect> BuildObstacleRects(string fromId, string toId, float nodeSize)
    {
        var obstacles = new List<AchievementTreeEdgeRouting.Rect>(_nodes.Count);

        foreach (var (id, node) in _nodes)
        {
            if (id == fromId || id == toId)
                continue;

            obstacles.Add(AchievementTreeEdgeRouting.NodeRect(node.Position, nodeSize));
        }

        return obstacles;
    }

    private bool IsRelatedEdge(string fromId, string toId)
    {
        return _selectedID != null && (fromId == _selectedID || toId == _selectedID);
    }

    private Color ResolveEdgeColor(AchievementTreeNode from, AchievementTreeNode to, bool related)
    {
        Color baseColor;
        if (related)
        {
            var focusState = _selectedID != null && _nodes.TryGetValue(_selectedID, out var selected)
                ? selected.State
                : to.State;

            baseColor = focusState switch
            {
                AchievementNodeState.Unlocked => Color.FromHex("#6EE7A0"),
                AchievementNodeState.Available => Color.FromHex("#8ADFFF"),
                _ => Color.FromHex("#C5D0DE"),
            };
            return baseColor;
        }

        baseColor = to.State switch
        {
            AchievementNodeState.Unlocked => Color.FromHex("#2F6B48"),
            AchievementNodeState.Available => Color.FromHex("#3A6E86"),
            _ => Color.FromHex("#2A2A2A"),
        };

        return baseColor.WithAlpha(_selectedID == null ? 0.85f : 0.28f);
    }
}
