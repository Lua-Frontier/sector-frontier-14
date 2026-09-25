// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Lua.Shared.Expedition;
using Content.Lua.Shared.Starmap;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Lua.UIKit.Shuttles;

public interface IStarMapConsoleScreen
{
    Control Control { get; }
    event Action<Star>? RequestWarpToStar;
    void Startup();
    void SetShuttle(EntityUid? shuttle);
    void SetConsole(EntityUid? console);
    void UpdateState(StarmapConsoleBoundUserInterfaceState state);
}

public interface IExpConsoleScreen
{
    Control Control { get; }
    event Action<ushort, int>? OnClaim;
    event Action? OnConfirm;
    event Action? OnCancel;
    event Action? OnFinish;
    void SetTabActive(bool active);
    void UpdateState(ExpeditionConsoleState? state);
}

public interface IShuttleConsoleLuaScreensFactory
{
    IStarMapConsoleScreen CreateStarMapScreen();
    IExpConsoleScreen CreateExpScreen();
}
