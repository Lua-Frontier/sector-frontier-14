// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Lua.UIKit.Performance;

public interface IHudPerfLabelFactory
{
    Control Create(IGameTiming gameTiming, IConfigurationManager cfg);
}
