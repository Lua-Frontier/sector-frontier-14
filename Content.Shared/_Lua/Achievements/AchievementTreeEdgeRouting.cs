// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;

namespace Content.Shared._Lua.Achievements;

public static class AchievementTreeEdgeRouting
{
    public const float BusStub = 18f;
    public const float ObstacleMargin = 6f;

    public readonly record struct Rect(float Left, float Top, float Right, float Bottom);

    public static List<Vector2> BuildPath(
        Vector2 fromPos,
        Vector2 toPos,
        float nodeSize,
        IReadOnlyList<Rect> obstacles)
    {
        var path = BuildBaseOrthogonalPath(fromPos, toPos, nodeSize);
        if (obstacles.Count == 0 || path.Count < 2)
            return path;

        var result = new List<Vector2> { path[0] };
        for (var i = 0; i < path.Count - 1; i++)
        {
            var segment = RouteSegment(result[^1], path[i + 1], obstacles);
            for (var j = 1; j < segment.Count; j++)
                result.Add(segment[j]);
        }

        return Simplify(result);
    }

    public static Rect NodeRect(Vector2 pos, float nodeSize, float margin = ObstacleMargin)
    {
        return new Rect(
            pos.X - margin,
            pos.Y - margin,
            pos.X + nodeSize + margin,
            pos.Y + nodeSize + margin);
    }

    private static List<Vector2> BuildBaseOrthogonalPath(Vector2 fromPos, Vector2 toPos, float nodeSize)
    {
        var fromCenter = fromPos + new Vector2(nodeSize / 2f, nodeSize / 2f);
        var toCenter = toPos + new Vector2(nodeSize / 2f, nodeSize / 2f);
        var dx = toCenter.X - fromCenter.X;
        var dy = toCenter.Y - fromCenter.Y;
        var points = new List<Vector2>(4);

        if (MathF.Abs(dy) < 8f && dx > 0f)
        {
            points.Add(new Vector2(fromPos.X + nodeSize, fromCenter.Y));
            points.Add(new Vector2(toPos.X, toCenter.Y));
            return points;
        }

        if (MathF.Abs(dy) < 8f && dx < 0f)
        {
            points.Add(new Vector2(fromPos.X, fromCenter.Y));
            points.Add(new Vector2(toPos.X + nodeSize, toCenter.Y));
            return points;
        }

        if (MathF.Abs(dx) < 8f)
        {
            if (dy > 0f)
            {
                points.Add(new Vector2(fromCenter.X, fromPos.Y + nodeSize));
                points.Add(new Vector2(toCenter.X, toPos.Y));
            }
            else
            {
                points.Add(new Vector2(fromCenter.X, fromPos.Y));
                points.Add(new Vector2(toCenter.X, toPos.Y + nodeSize));
            }

            return points;
        }

        if (dx > 0f)
        {
            var start = new Vector2(fromPos.X + nodeSize, fromCenter.Y);
            var end = new Vector2(toPos.X, toCenter.Y);
            var busX = fromPos.X + nodeSize + BusStub;
            busX = MathF.Min(busX, MathF.Max(start.X + 4f, end.X - 4f));

            points.Add(start);
            if (MathF.Abs(start.Y - end.Y) >= 1f)
            {
                points.Add(new Vector2(busX, start.Y));
                points.Add(new Vector2(busX, end.Y));
            }

            points.Add(end);
            return points;
        }

        {
            var start = new Vector2(fromPos.X, fromCenter.Y);
            var end = new Vector2(toPos.X + nodeSize, toCenter.Y);
            var busX = fromPos.X - BusStub;
            busX = MathF.Max(busX, MathF.Min(start.X - 4f, end.X + 4f));

            points.Add(start);
            if (MathF.Abs(start.Y - end.Y) >= 1f)
            {
                points.Add(new Vector2(busX, start.Y));
                points.Add(new Vector2(busX, end.Y));
            }

            points.Add(end);
            return points;
        }
    }

    private static List<Vector2> RouteSegment(Vector2 a, Vector2 b, IReadOnlyList<Rect> obstacles)
    {
        if (MathF.Abs(a.Y - b.Y) < 0.01f)
            return RouteHorizontal(a.X, a.Y, b.X, obstacles);

        if (MathF.Abs(a.X - b.X) < 0.01f)
            return RouteVertical(a.X, a.Y, b.Y, obstacles);

        return new List<Vector2> { a, b };
    }

    private static List<Vector2> RouteHorizontal(float x1, float y, float x2, IReadOnlyList<Rect> obstacles)
    {
        var forward = x2 >= x1;
        var spanLeft = MathF.Min(x1, x2);
        var spanRight = MathF.Max(x1, x2);

        var blockers = obstacles
            .Where(r => y >= r.Top && y <= r.Bottom && r.Left < spanRight && r.Right > spanLeft)
            .OrderBy(r => forward ? r.Left : -r.Left)
            .ToList();

        if (blockers.Count == 0)
            return new List<Vector2> { new(x1, y), new(x2, y) };

        var points = new List<Vector2> { new(x1, y) };
        var cx = x1;

        foreach (var blocker in blockers)
        {
            var before = forward ? blocker.Left - ObstacleMargin : blocker.Right + ObstacleMargin;
            var after = forward ? blocker.Right + ObstacleMargin : blocker.Left - ObstacleMargin;

            if (forward && before <= cx + 0.01f)
            {
                cx = MathF.Max(cx, after);
                continue;
            }

            if (!forward && before >= cx - 0.01f)
            {
                cx = MathF.Min(cx, after);
                continue;
            }

            points.Add(new(before, y));
            var detourY = PickDetourY(y, blocker);
            points.Add(new(before, detourY));
            points.Add(new(after, detourY));
            points.Add(new(after, y));
            cx = after;
        }

        if (MathF.Abs(cx - x2) > 0.01f)
            points.Add(new(x2, y));

        return Simplify(points);
    }

    private static List<Vector2> RouteVertical(float x, float y1, float y2, IReadOnlyList<Rect> obstacles)
    {
        var forward = y2 >= y1;
        var spanTop = MathF.Min(y1, y2);
        var spanBottom = MathF.Max(y1, y2);

        var blockers = obstacles
            .Where(r => x >= r.Left && x <= r.Right && r.Top < spanBottom && r.Bottom > spanTop)
            .OrderBy(r => forward ? r.Top : -r.Top)
            .ToList();

        if (blockers.Count == 0)
            return new List<Vector2> { new(x, y1), new(x, y2) };

        var points = new List<Vector2> { new(x, y1) };
        var cy = y1;

        foreach (var blocker in blockers)
        {
            var before = forward ? blocker.Top - ObstacleMargin : blocker.Bottom + ObstacleMargin;
            var after = forward ? blocker.Bottom + ObstacleMargin : blocker.Top - ObstacleMargin;

            if (forward && before <= cy + 0.01f)
            {
                cy = MathF.Max(cy, after);
                continue;
            }

            if (!forward && before >= cy - 0.01f)
            {
                cy = MathF.Min(cy, after);
                continue;
            }

            points.Add(new(x, before));
            var detourX = PickDetourX(x, blocker);
            points.Add(new(detourX, before));
            points.Add(new(detourX, after));
            points.Add(new(x, after));
            cy = after;
        }

        if (MathF.Abs(cy - y2) > 0.01f)
            points.Add(new(x, y2));

        return Simplify(points);
    }

    private static float PickDetourY(float y, Rect rect)
    {
        var above = rect.Top - ObstacleMargin;
        var below = rect.Bottom + ObstacleMargin;
        return MathF.Abs(y - above) <= MathF.Abs(y - below) ? above : below;
    }

    private static float PickDetourX(float x, Rect rect)
    {
        var left = rect.Left - ObstacleMargin;
        var right = rect.Right + ObstacleMargin;
        return MathF.Abs(x - left) <= MathF.Abs(x - right) ? left : right;
    }

    private static List<Vector2> Simplify(List<Vector2> points)
    {
        if (points.Count <= 2)
            return points;

        var result = new List<Vector2> { points[0] };
        for (var i = 1; i < points.Count - 1; i++)
        {
            var prev = result[^1];
            var curr = points[i];
            var next = points[i + 1];
            var collinearH = MathF.Abs(prev.Y - curr.Y) < 0.01f && MathF.Abs(curr.Y - next.Y) < 0.01f;
            var collinearV = MathF.Abs(prev.X - curr.X) < 0.01f && MathF.Abs(curr.X - next.X) < 0.01f;
            if (!collinearH && !collinearV)
                result.Add(curr);
        }

        result.Add(points[^1]);
        return result;
    }
}
