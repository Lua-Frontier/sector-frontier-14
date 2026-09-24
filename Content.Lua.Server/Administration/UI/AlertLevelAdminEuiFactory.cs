// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Administration;
using Content.Server.EUI;

namespace Content.Lua.Server.Administration.UI;

public sealed class AlertLevelAdminEuiFactory : IAlertLevelAdminEuiFactory
{
    public BaseEui Create() => new AlertLevelAdminEui();
}
