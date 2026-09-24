using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Shared._NF.Research;
using Content.Shared.Research.Prototypes;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Content.Client._NF.Research.UI;

/// <summary>
/// UI element for visualizing technologies prerequisites with configurable connection types
/// </summary>
public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    private readonly Dictionary<string, FancyResearchConsoleItem> _itemsById = new();
    private readonly List<(FancyResearchConsoleItem Prereq, FancyResearchConsoleItem Dependent)> _edges = new();
    private int _cachedChildrenHash;

    public HashSet<string>? FocusTechIds
    {
        get => _focusTechIds;
        set
        {
            _focusTechIds = value;
            ApplyFocusVisuals();
        }
    }

    private HashSet<string>? _focusTechIds;

    public Action<GUIMouseWheelEventArgs>? WheelMoved;
    public Action<GUIMouseMoveEventArgs>? MouseMoved;

    public ResearchesContainerPanel()
    {
    }

    public void SetFocusTech(string? techId)
    {
        if (techId == null)
        {
            FocusTechIds = null;
            return;
        }

        EnsureCache();
        if (!_itemsById.ContainsKey(techId))
        {
            FocusTechIds = null;
            return;
        }

        var forward = new Dictionary<string, List<string>>();
        var backward = new Dictionary<string, List<string>>();
        foreach (var (prereq, dependent) in _edges)
        {
            var from = prereq.Prototype.ID;
            var to = dependent.Prototype.ID;
            if (!forward.TryGetValue(from, out var outs))
                forward[from] = outs = new List<string>();
            outs.Add(to);
            if (!backward.TryGetValue(to, out var ins))
                backward[to] = ins = new List<string>();
            ins.Add(from);
        }

        var related = new HashSet<string> { techId };

        var up = new Queue<string>();
        up.Enqueue(techId);
        while (up.Count > 0)
        {
            var id = up.Dequeue();
            if (!backward.TryGetValue(id, out var parents))
                continue;
            foreach (var parent in parents)
            {
                if (related.Add(parent))
                    up.Enqueue(parent);
            }
        }

        var down = new Queue<string>();
        down.Enqueue(techId);
        while (down.Count > 0)
        {
            var id = down.Dequeue();
            if (!forward.TryGetValue(id, out var children))
                continue;
            foreach (var child in children)
            {
                if (related.Add(child))
                    down.Enqueue(child);
            }
        }

        FocusTechIds = related;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        WheelMoved?.Invoke(args);
        if (!args.Handled)
            base.MouseWheel(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        MouseMoved?.Invoke(args);
        base.MouseMove(args);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawPrerequisiteLines(handle);
        base.Draw(handle);
    }

    private void DrawPrerequisiteLines(DrawingHandleScreen handle)
    {
        EnsureCache();
        foreach (var (prereq, dependent) in _edges)
        {
            if (_focusTechIds is { } focus &&
                (!focus.Contains(prereq.Prototype.ID) || !focus.Contains(dependent.Prototype.ID)))
                continue;

            var startCoords = GetTechCenter(prereq);
            var endCoords = GetTechCenter(dependent);

            var lineColor = GetRefinedConnectionColor(prereq, dependent);
            if (_focusTechIds is { Count: > 0 })
                lineColor = new Color(lineColor.R, lineColor.G, lineColor.B, Math.Min(1f, lineColor.A * 1.15f));

            DrawBezierLine(handle, startCoords, endCoords, lineColor);
        }
    }

    private static void DrawBezierLine(DrawingHandleScreen handle, Vector2 start, Vector2 end, Color color)
    {
        var delta = end - start;
        var distance = delta.Length();
        if (distance < 1f)
            return;

        const float align = 14f;
        if (Math.Abs(delta.X) < align || Math.Abs(delta.Y) < align)
        {
            handle.DrawLine(start, end, color);
            return;
        }

        var mid = Vector2.Lerp(start, end, 0.5f);
        var along = delta / distance;
        var perp = new Vector2(-along.Y, along.X);
        var bend = Math.Clamp(distance * 0.22f, 12f, 64f);
        var side = Math.Abs(delta.Y) >= Math.Abs(delta.X)
            ? Math.Sign(delta.X == 0f ? 1f : delta.X)
            : Math.Sign(delta.Y == 0f ? 1f : -delta.Y);
        if (side == 0)
            side = 1;
        var offset = perp * bend * side;
        var c1 = Vector2.Lerp(start, mid, 0.35f) + offset * 0.55f;
        var c2 = Vector2.Lerp(mid, end, 0.65f) + offset * 0.55f;

        const int segments = 12;
        var prev = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1f - t;
            var point =
                u * u * u * start +
                3f * u * u * t * c1 +
                3f * u * t * t * c2 +
                t * t * t * end;
            handle.DrawLine(prev, point, color);
            prev = point;
        }
    }

    private void ApplyFocusVisuals()
    {
        EnsureCache();
        ApplyFocusVisualsCore();
    }

    private void ApplyFocusVisualsCore()
    {
        foreach (var item in _itemsById.Values)
        {
            item.Visible = true;
            if (_focusTechIds is null)
            {
                item.Modulate = Color.White;
                continue;
            }

            item.Modulate = _focusTechIds.Contains(item.Prototype.ID)
                ? Color.White
                : new Color(1f, 1f, 1f, 0.18f);
        }
    }

    private void EnsureCache()
    {
        var hash = 17;
        foreach (var child in Children)
            hash = unchecked(hash * 31 + RuntimeHelpers.GetHashCode(child));

        if (hash == _cachedChildrenHash)
            return;

        _cachedChildrenHash = hash;
        _itemsById.Clear();
        _edges.Clear();

        foreach (var child in Children)
        {
            if (child is FancyResearchConsoleItem item)
                _itemsById[item.Prototype.ID] = item;
        }

        foreach (var dependent in _itemsById.Values)
        {
            var prereqs = dependent.EffectivePrerequisites;
            if (prereqs.Count == 0)
                continue;

            foreach (var prereqId in prereqs)
            {
                if (_itemsById.TryGetValue(prereqId, out var prereq))
                    _edges.Add((prereq, dependent));
            }
        }
        ApplyFocusVisualsCore();
    }

    /// <summary>
    /// </summary>
    private Vector2 GetTechCenter(FancyResearchConsoleItem tech)
    {
        var techRect = GetTechRect(tech);
        return techRect.Center;
    }

    /// <summary>
    /// Get the visual rectangle of a tech item
    /// </summary>
    private UIBox2 GetTechRect(FancyResearchConsoleItem tech)
    {
        var position = new Vector2(tech.PixelPosition.X, tech.PixelPosition.Y);
        var size = new Vector2(tech.PixelWidth, tech.PixelHeight);

        var padding = 6f;
        return new UIBox2(
            position.X + padding,
            position.Y + padding,
            position.X + size.X - padding,
            position.Y + size.Y - padding
        );
    }

    /// <summary>
    /// Determine connection color based on availability
    /// </summary>
    private Color GetRefinedConnectionColor(FancyResearchConsoleItem prerequisite, FancyResearchConsoleItem dependent)
    {
        return ResearchColorScheme.GetConnectionColor(dependent.Availability);
    }

    /// <summary>
    /// Draw a clean line with subtle thickness
    /// </summary>
    private void DrawCleanLine(DrawingHandleScreen handle, Vector2 start, Vector2 end, Color color)
    {
        handle.DrawLine(start, end, color);

        // Add subtle thickness
        var direction = (end - start).Normalized();
        var perpendicular = new Vector2(-direction.Y, direction.X) * 0.5f;

        var thicknessColor = new Color(color.R, color.G, color.B, color.A * 0.6f);
        handle.DrawLine(start + perpendicular, end + perpendicular, thicknessColor);
        handle.DrawLine(start - perpendicular, end - perpendicular, thicknessColor);
    }
}
