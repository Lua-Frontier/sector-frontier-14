// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Power.Components;
using Content.Shared._Lua.StationTeleporter;
using Content.Shared._Lua.StationTeleporter.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Teleportation.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server._Lua.StationTeleporter;

public sealed class StationTeleporterSystem : SharedStationTeleporterSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    protected override bool IsUiOpen(EntityUid uid)
    { return _ui.IsAnyUiOpen(uid); }

    protected override void UpdateUserInterface(EntityUid uid, StationTeleporterComponent comp)
    {
        if (!TryComp<TransformComponent>(uid, out var xform)) return;
        if (xform.GridUid != null) EnsureComp<NavMapComponent>(xform.GridUid.Value);
        var teleporters = BuildTeleporterList(uid, comp, xform);
        var state = new StationTeleporterState(GetNetEntity(uid), comp.Type, teleporters);
        _ui.SetUiState(uid, StationTeleporterUIKey.Key, state);
    }

    private List<StationTeleporterStatus> BuildTeleporterList(EntityUid uid, StationTeleporterComponent comp, TransformComponent xform)
    {
        var result = new List<StationTeleporterStatus>();
        var query = EntityQueryEnumerator<StationTeleporterComponent, TransformComponent>();
        while (query.MoveNext(out var tpUid, out var tp, out var tpXform))
        {
            if (tp.Type != comp.Type) continue;
            if (tpUid == uid) continue;
            switch (comp.Type)
            {
                case TeleporterType.Local:
                    if (tpXform.GridUid != xform.GridUid) continue;
                    break;
                case TeleporterType.Sector:
                    if (tpXform.MapID != xform.MapID) continue;
                    break;

            }
            var coords = GetNetCoordinates(tpXform.Coordinates);
            NetCoordinates? linkedCoords = null;
            if (TryComp<LinkedEntityComponent>(tpUid, out var link) && link.LinkedEntities.Count > 0)
            {
                var partner = link.LinkedEntities.First();
                if (Exists(partner)) linkedCoords = GetNetCoordinates(Transform(partner).Coordinates);
            }

            var powered = TryComp<ApcPowerReceiverComponent>(tpUid, out var pwr) && pwr.Powered;
            var name = GetDisplayName(tpUid, tp, tpXform);
            result.Add(new StationTeleporterStatus(GetNetEntity(tpUid), coords, linkedCoords, name, powered));
        }
        return result;
    }

    private string GetDisplayName(EntityUid uid, StationTeleporterComponent tp, TransformComponent xform)
    {
        switch (tp.Type)
        {
            case TeleporterType.Local: return tp.CustomName ?? MetaData(uid).EntityName;
            case TeleporterType.Sector:
                if (xform.GridUid != null) return tp.CustomName ?? MetaData(xform.GridUid.Value).EntityName;
                return tp.CustomName ?? MetaData(uid).EntityName;
            default:
                return MetaData(uid).EntityName;
        }
    }
}
