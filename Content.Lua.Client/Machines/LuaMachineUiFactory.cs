// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.Lathe.UI;
using Content.Client.Lua.Research.UI;
using Content.Client.Lua.Shipyard.UI;
using Content.Client.Lua.VendingMachines;
using Content.Lua.UIKit.Machines;

namespace Content.Client.Lua.Machines;

public sealed class LuaMachineUiFactory : ILuaMachineUiFactory
{
    public ILunaLatheMenu CreateLatheMenu() => new LunaLatheMenu();
    public ILunaBlueprintLatheMenu CreateBlueprintLatheMenu() => new LunaBlueprintLatheMenu();
    public ILuaVendingMachineWindow CreateVendingMachineWindow() => new LuaVendingMachineWindow();
    public ILunaResearchConsoleMenu CreateResearchConsoleMenu() => new LunaResearchConsoleMenu();
    public IShipyardDockRadar CreateShipyardDockRadar() => new ShipyardDockRadarControl();
}
