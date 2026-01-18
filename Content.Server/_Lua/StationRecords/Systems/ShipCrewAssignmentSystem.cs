// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.PDA;
using Content.Server._Lua.StationRecords.Components;
using Content.Shared._Lua.StationRecords;
using Content.Shared._Lua.StationRecords.Components;
using Content.Shared.Access.Components;
using Content.Shared.PDA;

namespace Content.Server._Lua.StationRecords.Systems;

public sealed class ShipCrewAssignmentSystem : EntitySystem
{
    [Dependency] private readonly PdaSystem _pda = default!;

    public bool TryAssign(EntityUid targetIdCard, EntityUid shuttleUid, string shipName, ShipCrewRole role, out string? existingShipName)
    {
        existingShipName = null;
        if (!TryComp<IdCardComponent>(targetIdCard, out _)) return false;
        if (TryComp<ShipCrewAssignmentComponent>(targetIdCard, out var existing) && existing.ShuttleUid is { Valid: true } existingShip && existingShip != shuttleUid)
        {
            existingShipName = string.IsNullOrWhiteSpace(existing.ShipName) ? null : existing.ShipName;
            return false;
        }
        var assignment = EnsureComp<ShipCrewAssignmentComponent>(targetIdCard);
        assignment.ShuttleUid = shuttleUid;
        assignment.ShipName = shipName;
        assignment.Role = role;
        var status = EnsureComp<ShipCrewAssignmentStatusComponent>(targetIdCard);
        status.ShuttleUid = shuttleUid;
        status.Role = role;
        UpdateAllPdasContainingId(targetIdCard);
        return true;
    }

    public void Assign(EntityUid targetIdCard, EntityUid shuttleUid, string shipName, ShipCrewRole role)
    { TryAssign(targetIdCard, shuttleUid, shipName, role, out _); }

    public int ClearAllForShuttle(EntityUid shuttleUid)
    {
        var toClear = new List<EntityUid>();
        var query = EntityQueryEnumerator<ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var assignment))
        { if (assignment.ShuttleUid == shuttleUid) toClear.Add(uid); }
        foreach (var uid in toClear)
        {
            RemComp<ShipCrewAssignmentComponent>(uid);
            RemComp<ShipCrewAssignmentStatusComponent>(uid);
            UpdateAllPdasContainingId(uid);
        }
        return toClear.Count;
    }

    public int ClearForShuttleAndName(EntityUid shuttleUid, string fullName)
    {
        var toClear = new List<EntityUid>();
        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            var name = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            if (!string.Equals(name, fullName, StringComparison.Ordinal)) continue;
            if (assignment.ShuttleUid == shuttleUid) toClear.Add(uid);
        }
        foreach (var uid in toClear)
        {
            RemComp<ShipCrewAssignmentComponent>(uid);
            RemComp<ShipCrewAssignmentStatusComponent>(uid);
            UpdateAllPdasContainingId(uid);
        }
        return toClear.Count;
    }

    public bool TryGetAssignment(EntityUid idCard, out (string shipName, string roleLocKey) info)
    {
        info = default;
        if (!TryComp<ShipCrewAssignmentComponent>(idCard, out var assignment)) return false;
        info = (assignment.ShipName, ShipCrewManagement.GetRoleLocKey(assignment.Role));
        return true;
    }

    public List<ShipCrewRosterEntry> GetRosterForShuttle(EntityUid shuttleUid)
    {
        var roster = new List<ShipCrewRosterEntry>();
        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            if (assignment.ShuttleUid != shuttleUid) continue;
            var name = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            roster.Add(new ShipCrewRosterEntry(name, assignment.Role));
        }
        roster.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return roster;
    }

    public bool TrySetRoleForShuttleAndName(EntityUid shuttleUid, string name, ShipCrewRole role)
    {
        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            if (assignment.ShuttleUid != shuttleUid) continue;
            var idName = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            if (!string.Equals(idName, name, StringComparison.Ordinal)) continue;
            assignment.Role = role;
            if (TryComp<ShipCrewAssignmentStatusComponent>(uid, out var status)) status.Role = role;
            UpdateAllPdasContainingId(uid);
            return true;
        }
        return false;
    }

    private void UpdateAllPdasContainingId(EntityUid idCard)
    {
        var query = EntityQueryEnumerator<PdaComponent>();
        while (query.MoveNext(out var pdaUid, out var pda))
        {
            if (pda.ContainedId != idCard) continue;
            _pda.UpdatePdaUi(pdaUid, pda);
        }
    }
}

