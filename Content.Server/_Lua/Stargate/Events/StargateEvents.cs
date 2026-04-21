// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Stargate.Events;

[ByRefEvent]
public record struct AttemptStargateOpenEvent(EntityUid MapUid, EntityUid DestGateUid)
{
    public readonly EntityUid MapUid = MapUid;
    public readonly EntityUid DestGateUid = DestGateUid;
    public bool Cancelled = false;
}
[ByRefEvent]
public readonly record struct StargateOpenEvent(EntityUid MapUid, EntityUid SourceGateUid, EntityUid DestGateUid);
