// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.StationRecords.Components;
using Content.Server.Mind;
using Content.Server.PDA;
using Content.Shared._Lua.StationRecords;
using Content.Shared._Lua.StationRecords.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.StationRecords;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;

namespace Content.Server._Lua.StationRecords.Systems;

public sealed class ShipCrewAssignmentSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PdaSystem _pda = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

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
        assignment.AssignedUserId = null;
        TryRefreshAssignmentIdentity(targetIdCard, assignment);
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

    public bool TryRefreshAssignmentIdentity(EntityUid idCard, ShipCrewAssignmentComponent? assignment = null)
    {
        if (!Resolve(idCard, ref assignment, false))
            return false;

        if (assignment.AssignedUserId != null)
            return true;

        if (!TryResolveAssignedUserId(idCard, out var userId))
            return false;

        assignment.AssignedUserId = userId;
        return true;
    }

    public bool ForceRefreshAssignmentIdentity(EntityUid idCard, ShipCrewAssignmentComponent? assignment = null)
    {
        if (!Resolve(idCard, ref assignment, false))
            return false;

        TryResolveAssignedUserId(idCard, out var userId);
        assignment.AssignedUserId = userId;
        return userId != null;
    }

    private bool TryResolveAssignedUserId(EntityUid targetIdCard, out NetUserId? userId)
    {
        userId = null;

        if (!TryComp<StationRecordKeyStorageComponent>(targetIdCard, out var keyStorage) ||
            keyStorage.Key is not { } targetKey ||
            !targetKey.IsValid())
        {
            return false;
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame))
                continue;

            if (!_mind.TryGetMind(session.UserId, out _, out var mind) ||
                mind.UserId != session.UserId)
            {
                continue;
            }

            if (!MindMatchesRecordKey(mind, targetKey))
                continue;

            userId = session.UserId;
            return true;
        }

        return false;
    }

    private bool MindMatchesRecordKey(MindComponent mind, StationRecordKey targetKey)
    {
        return EntityMatchesRecordKey(mind.CurrentEntity, targetKey) || EntityMatchesRecordKey(mind.OwnedEntity, targetKey);
    }

    private bool EntityMatchesRecordKey(EntityUid? entityUid, StationRecordKey targetKey)
    {
        if (entityUid is not { Valid: true } uid)
            return false;

        if (!_accessReader.FindStationRecordKeys(uid, out var recordKeys))
            return false;

        foreach (var recordKey in recordKeys)
        {
            if (recordKey.Equals(targetKey))
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

