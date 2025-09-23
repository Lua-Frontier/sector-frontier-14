// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using JetBrains.Annotations;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Lua.Physics;

[UsedImplicitly]
public sealed class PhysicsTickrateSyncSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CVars.NetTickrate, OnTickrateChanged, true);
        var cur = _cfg.GetCVar(CVars.NetTickrate);
        _cfg.SetCVar(CVars.TargetMinimumTickrate, cur);
    }

    private void OnTickrateChanged(int rate, in CVarChangeInfo info)
    { _cfg.SetCVar(CVars.TargetMinimumTickrate, rate); }
}


