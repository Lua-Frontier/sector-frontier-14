using System.Collections.Generic;
using Content.Lua.Shared.StationRecords;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.StationRecords;

public interface IShipCrewAssignmentSystem : IEntitySystem
{
    bool TryGetAssignment(EntityUid idCard, out (string shipName, string roleLocKey) info);
    List<ShipCrewRosterEntry> GetRosterForShuttle(EntityUid shuttleUid);
    bool TryAssign(EntityUid targetIdCard, EntityUid shuttleUid, string shipName, ShipCrewRole role, out string? existingShipName);
    int ClearForShuttleAndName(EntityUid shuttleUid, string fullName);
    bool TrySetRoleForShuttleAndName(EntityUid shuttleUid, string name, ShipCrewRole role);
    int ClearAllForShuttle(EntityUid shuttleUid);
}
