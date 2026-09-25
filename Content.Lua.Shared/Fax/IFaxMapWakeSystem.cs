// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Fax;

public interface IFaxMapWakeSystem : IEntitySystem
{
    bool TryFindFaxByAddress(string address, out EntityUid faxUid);
    void EnsureAwake(EntityUid faxUid);
}
