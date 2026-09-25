// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.Expedition.UI;
using Content.Client.Lua.Starmap;
using Content.Lua.UIKit.Shuttles;

namespace Content.Client.Lua.Shuttles;

public sealed class ShuttleConsoleLuaScreensFactory : IShuttleConsoleLuaScreensFactory
{
    public IStarMapConsoleScreen CreateStarMapScreen() => new StarMapScreen();
    public IExpConsoleScreen CreateExpScreen() => new ExpScreen();
}
