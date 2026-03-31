// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Linq;
using Content.Shared._Lua.ShipCpu.Components;
using Content.Shared.Tiles;
using Robust.Shared.Map.Components;

namespace Content.Shared._Lua.ShipCpu;

public sealed class ShipCpuBuildLimitSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipCpuLimitComponent, FloorTileAttemptEvent>(OnFloorTileAttempt);
    }

    private void OnFloorTileAttempt(Entity<ShipCpuLimitComponent> ent, ref FloorTileAttemptEvent args)
    {
        if (args.Cancelled) return;
        var limit = ent.Comp;
        if (limit.Unlimited) return;
        if (!TryComp<MapGridComponent>(ent.Owner, out var mapGrid)) return;
        var tiles = _mapSystem.GetAllTiles(ent.Owner, mapGrid).ToList();
        var newPos = args.GridIndices;
        var currentTile = _mapSystem.GetTileRef(ent.Owner, mapGrid, newPos);
        var isNewTile = currentTile.Tile.IsEmpty;

        if (isNewTile)
        {
            if (tiles.Count >= limit.MaxTiles)
            {
                args.Cancelled = true;
                args.Reason = Loc.GetString("ship-cpu-build-blocked-tiles", ("current", tiles.Count), ("max", limit.MaxTiles));
                return;
            }
            int minX, maxX, minY, maxY;

            if (tiles.Count == 0)
            {
                minX = maxX = newPos.X;
                minY = maxY = newPos.Y;
            }
            else
            {
                minX = Math.Min(tiles.Min(t => t.X), newPos.X);
                maxX = Math.Max(tiles.Max(t => t.X), newPos.X);
                minY = Math.Min(tiles.Min(t => t.Y), newPos.Y);
                maxY = Math.Max(tiles.Max(t => t.Y), newPos.Y);
            }

            var newWidth = maxX - minX + 1;
            var newHeight = maxY - minY + 1;
            var newMaxSide = Math.Max(newWidth, newHeight);

            if (newMaxSide > limit.MaxSide)
            {
                args.Cancelled = true;
                args.Reason = Loc.GetString("ship-cpu-build-blocked-side", ("side", newMaxSide), ("max", limit.MaxSide));
            }
        }
    }
}
