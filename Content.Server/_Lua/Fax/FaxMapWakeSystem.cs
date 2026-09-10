// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Sectors;
using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Systems;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Fax.Components;
using Robust.Shared.Map;

namespace Content.Server._Lua.Fax;

public sealed class FaxMapWakeSystem : EntitySystem
{
    [Dependency] private readonly SectorIdleFreezeSystem _sectorIdleFreeze = default!;
    [Dependency] private readonly StargateMapFreezeSystem _stargateFreeze = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    /// <summary>
    /// Unfreezes the map hosting <paramref name="fax"/> so DeviceNetwork delivery and
    /// FaxSystem.Update printing can proceed. Idle freeze will re-pause empty sector maps later.
    /// </summary>
    public void EnsureAwake(EntityUid fax)
    {
        if (!TryComp(fax, out TransformComponent? xform))
            return;

        var mapId = xform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        _sectorIdleFreeze.EnsureUnfrozen(mapId);

        if (xform.MapUid is { } mapUid &&
            TryComp<StargateDestinationComponent>(mapUid, out var dest) &&
            dest.Frozen)
        {
            _stargateFreeze.Unfreeze(mapUid, dest);
        }

        if (_map.MapExists(mapId) && _map.IsPaused(mapId))
            _map.SetPaused(mapId, false);
    }

    public bool TryFindFaxByAddress(string address, out EntityUid faxUid)
    {
        faxUid = default;
        if (string.IsNullOrEmpty(address))
            return false;

        var query = AllEntityQuery<FaxMachineComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Address != address)
                continue;

            faxUid = uid;
            return true;
        }

        return false;
    }
}
