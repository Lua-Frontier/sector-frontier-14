// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Shipyard;

public interface IShipyardPreferencesReportSystem : IEntitySystem
{
    string? GetStatsPrintout(IReadOnlyList<int> roundBalances);
}
