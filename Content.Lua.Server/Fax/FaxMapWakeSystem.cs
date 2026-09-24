// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Lua.Shared.Fax;
using Content.Lua.Shared.Sectors;
using Content.Lua.Shared.Stargate;
using Content.Lua.Server.Stargate.Systems;
using Content.Shared.Fax.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Lua.Server.Fax;

public sealed class FaxMapWakeSystem : EntitySystem, IFaxMapWakeSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly ISectorIdleFreezeSystem _sectorIdleFreeze = default!;
    [Dependency] private readonly StargateMapFreezeSystem _stargateMapFreeze = default!;

    public bool TryFindFaxByAddress(string address, out EntityUid faxUid)
    {
        faxUid = default;
        if (string.IsNullOrEmpty(address))
            return false;

        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var uid, out var fax))
        {
            if (fax.FaxName != address)
                continue;

            faxUid = uid;
            return true;
        }

        return false;
    }

    public void EnsureAwake(EntityUid faxUid)
    {
        if (!TryComp(faxUid, out TransformComponent? xform))
            return;

        var mapId = xform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        _sectorIdleFreeze.EnsureUnfrozen(mapId);

        if (!_map.TryGetMap(mapId, out var mapUid) || mapUid == null)
            return;

        if (TryComp<StargateDestinationComponent>(mapUid.Value, out var dest))
            _stargateMapFreeze.Unfreeze(mapUid.Value, dest);

        if (TryComp<MapComponent>(mapUid.Value, out var mapComp) && mapComp.MapPaused)
            _map.SetPaused(mapUid.Value, false);
    }
}
