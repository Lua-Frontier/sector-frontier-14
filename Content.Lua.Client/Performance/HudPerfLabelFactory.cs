// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.Tick;
using Content.Client.Lua.UserInterface.Controls;
using Content.Lua.UIKit.Performance;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Lua.Performance;

public sealed class HudPerfLabelFactory : IHudPerfLabelFactory
{
    public Control Create(IGameTiming gameTiming, IConfigurationManager cfg)
    {
        var perf = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ClientServerPerfSystem>();
        return new HudPerfLabel(gameTiming, perf, cfg);
    }
}
