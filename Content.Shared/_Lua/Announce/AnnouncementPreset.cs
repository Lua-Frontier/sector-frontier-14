// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Announce;

[Serializable, NetSerializable]
public enum AnnouncementPreset : byte
{
    Comms,
    Faction,
    Alert,
    OnboardComputer
}
