// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
using Content.Shared._Lua.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    partial void SuppressImpactDamage(ref bool suppress, EntityUid uid, ShuttleComponent component, ref StartCollideEvent args)
    {
        if (suppress) return;
        if (!_gridQuery.TryComp(args.OurEntity, out var ourGrid) || !_gridQuery.TryComp(args.OtherEntity, out var otherGrid)) { return; }
        var ourXform = Transform(args.OurEntity);
        var otherXform = Transform(args.OtherEntity);
        if (ourXform.MapUid == null || otherXform.MapUid == null) return;
        var anyMagContact = false;
        foreach (var worldPoint in args.WorldPoints)
        {
            if (!anyMagContact)
            {
                var ourPoint = _transform.ToCoordinates((args.OurEntity, ourXform), new MapCoordinates(worldPoint, ourXform.MapID));
                var ourTile = new Vector2i((int)Math.Floor(ourPoint.X / ourGrid.TileSize), (int)Math.Floor(ourPoint.Y / ourGrid.TileSize));
                _intersecting.Clear();
                _lookup.GetLocalEntitiesIntersecting(args.OurEntity, ourTile, _intersecting, gridComp: ourGrid);
                foreach (var ent in _intersecting)
                {
                    if (HasComp<MagneticGrabberComponent>(ent) || HasComp<MagneticLatchComponent>(ent))
                    {
                        anyMagContact = true;
                        break;
                    }
                }
            }
            if (!anyMagContact)
            {
                var otherPoint = _transform.ToCoordinates((args.OtherEntity, otherXform), new MapCoordinates(worldPoint, otherXform.MapID));
                var otherTile = new Vector2i((int)Math.Floor(otherPoint.X / otherGrid.TileSize), (int)Math.Floor(otherPoint.Y / otherGrid.TileSize));
                _intersecting.Clear();
                _lookup.GetLocalEntitiesIntersecting(args.OtherEntity, otherTile, _intersecting, gridComp: otherGrid);
                foreach (var ent in _intersecting)
                {
                    if (HasComp<MagneticGrabberComponent>(ent) || HasComp<MagneticLatchComponent>(ent))
                    {
                        anyMagContact = true;
                        break;
                    }
                }
            }
            if (anyMagContact)
            {
                suppress = true;
                return;
            }
        }
    }
}

