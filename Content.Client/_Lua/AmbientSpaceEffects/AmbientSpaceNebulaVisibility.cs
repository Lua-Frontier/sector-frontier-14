// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.AmbientSpaceEffects;

internal sealed class AmbientSpaceNebulaVisibility
{
    private readonly IMapManager _mapManager;
    private readonly IPrototypeManager _prototypes;
    private readonly SharedMapSystem _map;
    private readonly TurfSystem _turf;

    private List<Entity<MapGridComponent>> _grids = new();

    public AmbientSpaceNebulaVisibility(
        IEntityManager entManager,
        IMapManager mapManager,
        IPrototypeManager prototypes)
    {
        _mapManager = mapManager;
        _prototypes = prototypes;
        _map = entManager.System<SharedMapSystem>();
        _turf = entManager.System<TurfSystem>();
    }

    public Box2 GetPotentialDrawBounds(AmbientSpaceFieldComponent field, Vector2 fieldPos, Vector2 eyePos, float radius)
    {
        var size = new Vector2(radius * 2f, radius * 2f);
        var bounds = Box2.CenteredAround(fieldPos, size);

        if (!_prototypes.TryIndex<AmbientSpaceEffectPrototype>(field.Effect, out var effect))
            return bounds;

        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.LowerParallax), size));
        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.MidParallax), size));
        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.UpperParallax), size));
        return bounds;
    }

    public bool HasVisibleMidLayer(MapId mapId, Vector2 worldPos, ReadOnlySpan<Vector2> contourPoints)
    {
        if (contourPoints.Length == 0)
            return false;

        var visibleSamples = 0;
        var step = Math.Max(1, contourPoints.Length / 8);
        for (var i = 0; i < contourPoints.Length; i += step)
        {
            if (!IsBlockedAt(mapId, worldPos + contourPoints[i]))
                visibleSamples++;
        }

        if (!IsBlockedAt(mapId, worldPos))
            visibleSamples++;

        return visibleSamples > 0;
    }

    public static Vector2 GetParallaxDrawPos(Vector2 fieldPos, Vector2 eyePos, float parallax)
    {
        return fieldPos + (eyePos - fieldPos) * (1f - parallax);
    }

    private bool IsBlockedAt(MapId mapId, Vector2 worldPos)
    {
        _grids.Clear();
        var pointBounds = Box2.CenteredAround(worldPos, new Vector2(0.2f, 0.2f));
        _mapManager.FindGridsIntersecting(mapId, pointBounds, ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            var indices = _map.WorldToTile(grid.Owner, grid.Comp, worldPos);

            if (!_map.TryGetTileRef(grid.Owner, grid.Comp, indices, out var tile))
                continue;

            if (ShouldBlockNebula(tile))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Block nebula on any tile surface. Only empty / space tiles stay open.
    /// </summary>
    public bool ShouldBlockNebula(TileRef tile)
    {
        return !tile.Tile.IsEmpty && !_turf.IsSpace(tile);
    }
}
