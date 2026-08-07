// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.StationEvents.Events;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lua.SpaceHazards;

public static class SectorBlackHoleConsume
{
    public const int SwallowTileThreshold = 48;
    public const float SwallowInsideFraction = 0.45f;
    public const float PullMul = 1.25f;

    private static readonly List<(Vector2i, Tile)> TileScratch = new(128);
    private static readonly List<EntityUid> AnchoredScratch = new(32);
    private static readonly HashSet<EntityUid> FreeScratch = new(64);

    public static bool TryEraseAndMaybeSwallow(
        EntityUid holeUid,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldCenter,
        float horizonRadius,
        SharedMapSystem maps,
        SharedTransformSystem transform,
        IEntityManager entMan,
        LinkedLifecycleGridSystem linkedLifecycle)
    {
        if (entMan.Deleted(gridUid) || entMan.IsQueuedForDeletion(gridUid))
            return true;

        if (horizonRadius <= 0f)
            return false;

        var circle = new Circle(worldCenter, horizonRadius);
        var r2 = horizonRadius * horizonRadius;

        TileScratch.Clear();

        foreach (var tileRef in maps.GetTilesIntersecting(gridUid, grid, circle))
        {
            if (tileRef.Tile.IsEmpty)
                continue;

            var center = maps.GridTileToWorldPos(gridUid, grid, tileRef.GridIndices);
            if ((center - worldCenter).LengthSquared() > r2)
                continue;

            DeleteAnchoredOnTile(gridUid, grid, maps, entMan, tileRef.GridIndices);
            TileScratch.Add((tileRef.GridIndices, Tile.Empty));
        }

        if (TileScratch.Count > 0)
            maps.SetTiles(gridUid, grid, TileScratch);

        if (entMan.Deleted(gridUid) || entMan.IsQueuedForDeletion(gridUid))
            return true;

        CountTiles(gridUid, grid, maps, worldCenter, horizonRadius, out var totalTiles, out var insideExact);

        if (ShouldSwallowGrid(totalTiles, insideExact))
        {
            SwallowGrid(gridUid, entMan, linkedLifecycle);
            return true;
        }

        return false;
    }

    public static bool ShouldSwallowGrid(int totalTiles, int tilesInsideHorizon)
    {
        if (totalTiles <= 0)
            return true;

        if (totalTiles <= SwallowTileThreshold)
            return true;

        if (tilesInsideHorizon <= 0)
            return false;

        return tilesInsideHorizon >= totalTiles * SwallowInsideFraction;
    }

    public static void SwallowGrid(
        EntityUid gridUid,
        IEntityManager entMan,
        LinkedLifecycleGridSystem linkedLifecycle)
    {
        if (entMan.Deleted(gridUid) || entMan.IsQueuedForDeletion(gridUid))
            return;

        linkedLifecycle.UnparentPlayersFromGrid(gridUid, deleteGrid: true);
    }

    public static void PullAndConsumeFreeEntities(
        EntityUid holeUid,
        MapId mapId,
        Vector2 worldCenter,
        float pullRadius,
        float horizonRadius,
        float pullAcceleration,
        float dt,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        SharedPhysicsSystem physics,
        IEntityManager entMan)
    {
        if (mapId == MapId.Nullspace || pullRadius <= 0f)
            return;

        var consumeR2 = horizonRadius * horizonRadius;
        var pullR2 = pullRadius * pullRadius;

        FreeScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, pullRadius, FreeScratch, LookupFlags.Uncontained);

        foreach (var uid in FreeScratch)
        {
            if (uid == holeUid)
                continue;

            if (!CanConsumeFreeEntity(uid, entMan))
                continue;

            var pos = transform.GetWorldPosition(uid);
            var distSq = (worldCenter - pos).LengthSquared();
            if (distSq > pullR2)
                continue;

            if (distSq <= consumeR2)
            {
                entMan.DeleteEntity(uid);
                continue;
            }

            if (!entMan.HasComponent<MobStateComponent>(uid))
                continue;

            if (!entMan.TryGetComponent(uid, out PhysicsComponent? body) || body.BodyType == BodyType.Static)
                continue;

            var delta = worldCenter - pos;
            var d = MathF.Sqrt(distSq);
            var dir = delta / d;
            var t = Math.Clamp(1f - d / pullRadius, 0f, 1f);
            var strength = pullAcceleration * PullMul * (0.25f * t + 0.9f * t * t);
            var vel = body.LinearVelocity + dir * strength * dt;

            var outward = Vector2.Dot(vel, -dir);
            if (outward > 0f)
                vel += dir * outward;

            physics.SetLinearVelocity(uid, vel, body: body);
        }
    }

    public static bool CanConsumeFreeEntity(EntityUid uid, IEntityManager entMan)
    {
        if (entMan.Deleted(uid) || entMan.IsQueuedForDeletion(uid))
            return false;

        if (entMan.HasComponent<MapGridComponent>(uid))
            return false;

        if (entMan.HasComponent<GhostComponent>(uid))
            return false;

        if (entMan.HasComponent<SectorCelestialBodyComponent>(uid))
            return false;

        if (entMan.HasComponent<SectorBackgroundPlanetComponent>(uid))
            return false;

        if (entMan.HasComponent<AmbientSpaceFieldComponent>(uid))
            return false;

        return true;
    }

    private static void DeleteAnchoredOnTile(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        IEntityManager entMan,
        Vector2i indices)
    {
        AnchoredScratch.Clear();
        var enumerator = maps.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        while (enumerator.MoveNext(out var entNullable))
        {
            if (entNullable is not { } ent)
                continue;

            if (!CanConsumeFreeEntity(ent, entMan))
                continue;

            AnchoredScratch.Add(ent);
        }

        foreach (var ent in AnchoredScratch)
            entMan.DeleteEntity(ent);
    }

    private static void CountTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        Vector2 worldCenter,
        float horizonRadius,
        out int total,
        out int inside)
    {
        total = 0;
        inside = 0;
        var r2 = horizonRadius * horizonRadius;
        var enumerator = maps.GetAllTilesEnumerator(gridUid, grid);
        while (enumerator.MoveNext(out var tileRefNullable))
        {
            if (tileRefNullable is not { } tileRef)
                continue;

            if (tileRef.Tile.IsEmpty)
                continue;

            total++;
            var center = maps.GridTileToWorldPos(gridUid, grid, tileRef.GridIndices);
            if ((center - worldCenter).LengthSquared() <= r2)
                inside++;
        }
    }
}
