// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public static class SectorCelestialHullDamage
{
    public static void DamageInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldCenter,
        float radius,
        DamageSpecifier damage,
        int maxHits,
        SharedMapSystem maps,
        TurfSystem turf,
        DamageableSystem damageable,
        IEntityManager entMan,
        IRobustRandom random)
    {
        if (damage.Empty || maxHits <= 0 || radius <= 0f)
            return;

        var damaged = 0;
        var attempts = maxHits * 16;
        var r2 = radius * radius;

        for (var i = 0; i < attempts && damaged < maxHits; i++)
        {
            if (!TryPickExteriorTileInRadius(gridUid, grid, worldCenter, r2, maps, turf, random, out var tile))
                continue;

            if (!TryDamageTile(gridUid, grid, maps, damageable, entMan, damage, tile))
                continue;

            damaged++;
        }
    }

    public static bool TryDamageRandomOnGrid(
        EntityUid gridUid,
        MapGridComponent grid,
        DamageSpecifier damage,
        SharedMapSystem maps,
        TurfSystem turf,
        DamageableSystem damageable,
        IEntityManager entMan,
        IRobustRandom random)
    {
        if (damage.Empty)
            return false;

        if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, maps, turf, random, out var tile))
            return false;

        return TryDamageTile(gridUid, grid, maps, damageable, entMan, damage, tile);
    }

    public static void ClearBareTilesInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldCenter,
        float radius,
        int maxClears,
        SharedMapSystem maps,
        TurfSystem turf,
        IEntityManager entMan,
        IRobustRandom random)
    {
        if (maxClears <= 0 || radius <= 0f)
            return;

        var cleared = 0;
        var attempts = maxClears * 16;
        var r2 = radius * radius;
        var toClear = new List<(Vector2i, Tile)>();
        var used = new HashSet<Vector2i>();

        for (var i = 0; i < attempts && cleared < maxClears; i++)
        {
            if (!TryPickExteriorTileInRadius(gridUid, grid, worldCenter, r2, maps, turf, random, out var tile))
                continue;

            if (!used.Add(tile))
                continue;

            if (!maps.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (HasLiveAnchored(gridUid, grid, maps, entMan, tile))
                continue;

            toClear.Add((tile, Tile.Empty));
            cleared++;
        }

        if (toClear.Count > 0)
            maps.SetTiles(gridUid, grid, toClear);
    }

    private static bool HasLiveAnchored(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        IEntityManager entMan,
        Vector2i tile)
    {
        var enumerator = maps.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (enumerator.MoveNext(out var entNullable))
        {
            if (entNullable is not { } ent)
                continue;

            if (entMan.Deleted(ent) || entMan.IsQueuedForDeletion(ent))
                continue;

            return true;
        }

        return false;
    }

    private static bool TryDamageTile(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        DamageableSystem damageable,
        IEntityManager entMan,
        DamageSpecifier damage,
        Vector2i tile)
    {
        var enumerator = maps.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        var anyHit = false;

        while (enumerator.MoveNext(out var entNullable))
        {
            if (entNullable is not { } ent)
                continue;

            if (!entMan.TryGetComponent(ent, out DamageableComponent? dmg))
                continue;

            var delta = damageable.TryChangeDamage(
                ent,
                damage,
                ignoreResistances: true,
                interruptsDoAfters: false,
                damageable: dmg);

            if (delta != null && delta.GetTotal() > 0)
                anyHit = true;
        }

        return anyHit;
    }

    private static bool TryPickExteriorTileInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldCenter,
        float radiusSquared,
        SharedMapSystem maps,
        TurfSystem turf,
        IRobustRandom random,
        out Vector2i tile)
    {
        var bounds = grid.LocalAABB;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            tile = default;
            return false;
        }

        var minX = (int) bounds.Left;
        var maxX = (int) Math.Max(bounds.Left + 1, bounds.Right);
        var minY = (int) bounds.Bottom;
        var maxY = (int) Math.Max(bounds.Bottom + 1, bounds.Top);

        for (var i = 0; i < 64; i++)
        {
            var candidate = new Vector2i(random.Next(minX, maxX), random.Next(minY, maxY));
            if (!GridHullExteriorHelper.IsExteriorHullTile(gridUid, grid, candidate, maps, turf))
                continue;

            var center = maps.GridTileToWorldPos(gridUid, grid, candidate);
            if ((center - worldCenter).LengthSquared() > radiusSquared)
                continue;

            tile = candidate;
            return true;
        }

        tile = default;
        return false;
    }
}
