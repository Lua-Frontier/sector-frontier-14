// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Shuttles.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Client._Lua.StationTeleporter;

public sealed class StationTeleporterSectorMapControl : Control
{
    public readonly ShuttleMapControl InnerMap;
    private readonly TeleporterLinkOverlay _overlay;

    public List<(MapCoordinates A, MapCoordinates B)> LinkedTeleporterPairs
    {
        get => _overlay.LinkedTeleporterPairs;
        set => _overlay.LinkedTeleporterPairs = value;
    }

    public StationTeleporterSectorMapControl()
    {
        InnerMap = new ShuttleMapControl();
        _overlay = new TeleporterLinkOverlay(InnerMap);

        AddChild(InnerMap);
        AddChild(_overlay);
        MouseFilter = MouseFilterMode.Pass;
        _overlay.MouseFilter = MouseFilterMode.Ignore;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var side = Math.Min(finalSize.X, finalSize.Y);
        var square = new Vector2(side, side);
        var box = UIBox2.FromDimensions(Vector2.Zero, square);
        InnerMap.Arrange(box);
        _overlay.Arrange(box);
        return square;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var side = availableSize.X;
        var square = new Vector2(side, side);
        InnerMap.Measure(square);
        _overlay.Measure(square);
        return square;
    }
    private sealed class TeleporterLinkOverlay : Control
    {
        private readonly ShuttleMapControl _map;
        public List<(MapCoordinates A, MapCoordinates B)> LinkedTeleporterPairs = new();
        public TeleporterLinkOverlay(ShuttleMapControl map)
        { _map = map; }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (LinkedTeleporterPairs.Count == 0) return;
            var worldRange = _map.WorldRange;
            if (worldRange <= 0f) return;
            var px = Math.Min(_map.PixelWidth, _map.PixelHeight);
            var midPoint = px / 2f;
            var minimapScale = midPoint / worldRange;
            var midVec = new Vector2(midPoint, midPoint);
            var offset = _map.Offset;
            var matty = Matrix3Helpers.CreateInverseTransform(offset, Angle.Zero);
            foreach (var (a, b) in LinkedTeleporterPairs)
            {
                if (a.MapId != b.MapId || a.MapId == MapId.Nullspace) continue;
                var posA = Vector2.Transform(a.Position, matty);
                posA = posA with { Y = -posA.Y };
                posA = posA * minimapScale + midVec;
                var posB = Vector2.Transform(b.Position, matty);
                posB = posB with { Y = -posB.Y };
                posB = posB * minimapScale + midVec;
                handle.DrawLine(posA, posB, Color.FromHex("#81ddeb"));
            }
        }
    }
}
