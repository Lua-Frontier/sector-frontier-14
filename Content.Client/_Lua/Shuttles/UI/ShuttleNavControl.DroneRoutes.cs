// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private void DrawDroneRoutes(DrawingHandleScreen handle, Matrix3x2 worldToView)
    {
        if (_droneRoutes == null || _droneRoutes.Count == 0)
            return;

        var timing = IoCManager.Resolve<IGameTiming>();
        var animOffset = (float) timing.RealTime.TotalSeconds * 30f;
        var color = Color.Cyan.WithAlpha(0.7f);

        foreach (var route in _droneRoutes)
        {
            if (_droneRouteFilter != null && !_droneRouteFilter.Contains(route.Steerer))
                continue;

            if (_droneRouteFilter is { Count: 0 })
                continue;

            if (route.Points.Count < 2)
                continue;

            Vector2? prev = null;
            foreach (var netCoords in route.Points)
            {
                var coords = EntManager.GetCoordinates(netCoords);
                var mapCoords = _transform.ToMapCoordinates(coords);
                if (mapCoords.MapId == MapId.Nullspace)
                {
                    prev = null;
                    continue;
                }

                var ui = Vector2.Transform(mapCoords.Position, worldToView);
                if (prev != null)
                    handle.DrawDottedLine(prev.Value, ui, color, animOffset, dashSize: 6f, gapSize: 3f);
                prev = ui;
            }
        }
    }
}
