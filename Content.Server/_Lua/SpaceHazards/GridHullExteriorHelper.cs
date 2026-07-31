// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public static class GridHullExteriorHelper
{
    private static readonly Vector2i[] Cardinals =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    public static bool IsExteriorHullTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices,
        SharedMapSystem maps,
        TurfSystem turf)
    {
        if (!maps.TryGetTileRef(gridUid, grid, indices, out var tileRef))
            return false;

        if (tileRef.Tile.IsEmpty || turf.IsSpace(tileRef))
            return false;

        foreach (var offset in Cardinals)
        {
            var neighbor = indices + offset;
            if (!maps.TryGetTileRef(gridUid, grid, neighbor, out var neighborRef))
                return true;

            if (neighborRef.Tile.IsEmpty || turf.IsSpace(neighborRef))
                return true;
        }

        return false;
    }

    public static bool TryPickRandomExteriorTile(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        TurfSystem turf,
        IRobustRandom random,
        out Vector2i tile,
        int maxAttempts = 48)
    {
        var bounds = grid.LocalAABB;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            tile = default;
            return false;
        }

        var minX = (int) bounds.Left;
        var maxX = (int) bounds.Right;
        var minY = (int) bounds.Bottom;
        var maxY = (int) bounds.Top;

        for (var i = 0; i < maxAttempts; i++)
        {
            var candidate = new Vector2i(random.Next(minX, maxX), random.Next(minY, maxY));
            if (IsExteriorHullTile(gridUid, grid, candidate, maps, turf))
            {
                tile = candidate;
                return true;
            }
        }

        tile = default;
        return false;
    }

    public static Vector2 TileCenterWorld(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        Vector2i tile)
    {
        return maps.GridTileToWorldPos(gridUid, grid, tile);
    }
}
