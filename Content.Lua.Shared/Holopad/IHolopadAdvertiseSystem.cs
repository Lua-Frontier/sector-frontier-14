// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Holopad;

public interface IHolopadAdvertiseSystem : IEntitySystem
{
    void UpdateScriptedBroadcasts();

    bool TryStartScriptedBroadcast(EntityUid holopadUid, EntityUid? actor);
}
