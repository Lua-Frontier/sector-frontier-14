// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.Options.UI.Tabs;
using Content.Lua.UIKit.Options;
using Robust.Client.UserInterface;

namespace Content.Lua.Client.Options;

public sealed class NetworkOptionsTabFactory : INetworkOptionsTabFactory
{
    public Control Create() => new NetworkTab();
}
