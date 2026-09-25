// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.EUI;

namespace Content.Server.Administration;

public interface IAlertLevelAdminEuiFactory
{
    BaseEui Create();
}
