// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Popups;
using Content.Server._Goobstation.SpaceWhale;
using Content.Shared._Goobstation.SpaceWhale;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.SpaceWhale;

public sealed class SpaceWhaleTileDevourSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    private readonly HashSet<EntityUid> _entityBuffer = new();
    private readonly HashSet<EntityUid> _popupBuffer = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpaceWhaleTileDevourComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var devour, out var xform))
        {
            devour.Accumulator += frameTime;
            if (devour.Accumulator < devour.DevourInterval)
                continue;

            devour.Accumulator -= devour.DevourInterval;
            _entityBuffer.Clear();
            _lookup.GetEntitiesInRange(uid, 3.0f, _entityBuffer, LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Approximate);

            var entsDevoured = 0;
            var anyDevour = false;

            foreach (var ent in _entityBuffer)
            {
                if (entsDevoured >= devour.EntitiesPerBite) break;
                if (ent == uid) continue;
                if (HasComp<SpaceWhaleTileDevourComponent>(ent) || HasComp<TailedEntityComponent>(ent)) continue;
                if (HasComp<MapComponent>(ent) || HasComp<MapGridComponent>(ent)) continue;
                if (_whitelist.IsWhitelistPass(devour.IgnoreWhitelist, ent)) continue;
                if (!TryComp<TransformComponent>(ent, out var entXform)) continue;
                if (entXform.MapUid == null) continue;
                if (_damageable.TryChangeDamage(ent, devour.Damage, interruptsDoAfters: false, origin: uid) != null)
                {
                    entsDevoured++;
                    anyDevour = true;
                }
            }
            if (xform.GridUid is not { Valid: true } gridUid) continue;
            if (!TryComp<MapGridComponent>(gridUid, out var grid)) continue;
            var tilesDevoured = 0;
            for (var dx = -1; dx <= 1 && tilesDevoured < devour.TilesPerBite; dx++)
            {
                for (var dy = -1; dy <= 1 && tilesDevoured < devour.TilesPerBite; dy++)
                {
                    var coords = xform.Coordinates.Offset(new Vector2(dx, dy));
                    var tileRef = _map.GetTileRef(gridUid, grid, coords);
                    if (tileRef.Tile.IsEmpty) continue;
                    _map.SetTile(gridUid, grid, coords, Tile.Empty);
                    tilesDevoured++;
                    anyDevour = true;
                }
            }
            if (anyDevour && HasComp<SpaceWhaleComponent>(uid) && _timing.CurTime >= devour.NextDevourPopup)
            {
                devour.NextDevourPopup = _timing.CurTime + TimeSpan.FromSeconds(10);

                _popupBuffer.Clear();
                _lookup.GetEntitiesInRange(uid, 20f, _popupBuffer, LookupFlags.Dynamic | LookupFlags.Approximate);
                foreach (var ent in _popupBuffer)
                {
                    if (!HasComp<ActorComponent>(ent)) continue;
                    _popup.PopupEntity(Loc.GetString("space-whale-devour"), ent, ent, PopupType.MediumCaution);
                }
            }
        }
    }
}

